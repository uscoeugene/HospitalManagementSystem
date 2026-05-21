using HMS.UI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IdentityModel.Tokens.Jwt;


var builder = WebApplication.CreateBuilder(args);

// Configuration
var apiBase = builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7142/";

// Add both Razor Pages and MVC controllers with views so we can use either approach
builder.Services.AddRazorPages().AddRazorRuntimeCompilation();
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
builder.Services.AddHttpContextAccessor();
// register antiforgery provider so we can support authenticated identities without Name claim
builder.Services.AddSingleton<Microsoft.AspNetCore.Antiforgery.IAntiforgeryAdditionalDataProvider, HMS.UI.Services.AntiforgeryAdditionalDataProvider>();
builder.Services.AddScoped<RefreshService>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHttpClient("HmsApi", client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["HmsApi:BaseUrl"] ?? "https://localhost:7142");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        // WARNING: only for local development
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });
}
else
{
    builder.Services.AddHttpClient("HmsApi", client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["HmsApi:BaseUrl"] ?? "https://api.example.com");
    });
}

builder.Services.AddScoped<HMS.UI.Services.ApiClient>();
// Static data service depends on ApiClient (scoped) so register as scoped as well
builder.Services.AddScoped<HMS.UI.Services.StaticDataService>();

// Add simple cookie authentication so MVC Challenge/Authorize can work in the UI

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;
    })
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Error/403";

    options.Events = new CookieAuthenticationEvents
    {
        OnValidatePrincipal = context =>
        {
            var token = context.Request.Cookies["HmsAuth"];

            if (string.IsNullOrWhiteSpace(token))
            {
                context.RejectPrincipal();
                context.Response.Redirect("/Account/Login");
                return Task.CompletedTask;
            }

            var handler = new JwtSecurityTokenHandler();

            try
            {
                var jwt = handler.ReadJwtToken(token);

                if (jwt.ValidTo < DateTime.UtcNow)
                {
                    context.RejectPrincipal();
                    context.Response.Redirect("/Account/Login");
                }
            }
            catch
            {
                context.RejectPrincipal();
                context.Response.Redirect("/Account/Login");
            }

            return Task.CompletedTask;
        }
    };
    //options.Events = new CookieAuthenticationEvents
    //{
    //    OnValidatePrincipal = async context =>
    //    {
    //        var token = context.Request.Cookies["HmsAuth"];

    //        if (string.IsNullOrWhiteSpace(token))
    //        {
    //            context.RejectPrincipal();
    //            await context.HttpContext.SignOutAsync();
    //            return;
    //        }

    //        var handler = new JwtSecurityTokenHandler();

    //        try
    //        {
    //            var jwt = handler.ReadJwtToken(token);

    //            if (jwt.ValidTo < DateTime.UtcNow)
    //            {
    //                context.RejectPrincipal();
    //                await context.HttpContext.SignOutAsync();
    //            }
    //        }
    //        catch
    //        {
    //            context.RejectPrincipal();
    //            await context.HttpContext.SignOutAsync();
    //        }
    //    }
    //};
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();   // REQUIRED
app.UseAuthorization();    // REQUIRED
app.UseMiddleware<HMS.UI.Middleware.AutoRefreshMiddleware>();

// Render friendly error pages for common status codes
app.UseStatusCodePagesWithReExecute("/Error/{0}");

// Map MVC controller routes (default) first so controller-based views take precedence
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Dashboard}/{id?}");

// convenient tenant-aware login URL: /login/{tenantCode}
app.MapControllerRoute(
    name: "tenantLogin",
    pattern: "login/{tenantCode?}",
    defaults: new { controller = "Account", action = "Login" });

app.MapGet("/", async context =>
{
    if (context.User.Identity?.IsAuthenticated ?? false)
    {
        context.Response.Redirect("/Account/Dashboard");
    }
    else
    {
        context.Response.Redirect("/Account/Login");
    }

    await Task.CompletedTask;
});
// Also map Razor Pages for any remaining pages
app.MapRazorPages();

app.Run();
