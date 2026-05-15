using Microsoft.AspNetCore.Builder;
using HMS.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


var builder = WebApplication.CreateBuilder(args);

// Configuration
var apiBase = builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7142/";

// Add both Razor Pages and MVC controllers with views so we can use either approach
builder.Services.AddRazorPages().AddRazorRuntimeCompilation();
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
builder.Services.AddHttpContextAccessor();
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

// Add simple cookie authentication so MVC Challenge/Authorize can work in the UI
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Error/403";
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

// Also map Razor Pages for any remaining pages
app.MapRazorPages();

app.Run();
