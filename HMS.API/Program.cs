using System.Text;
using HMS.API.Application.Auth;
using HMS.API.Application.Common;
using HMS.API.Infrastructure.Auth;
using HMS.API.Infrastructure.Persistence;
using HMS.API.Middleware;
using HMS.API.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using HMS.API.Infrastructure.Outbox;
using HMS.API.Hubs;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure DbContexts
var conn = builder.Configuration.GetConnectionString("Default") ?? "Server=(localdb)\\MSSQLLocalDB;Database=HmsDb;Trusted_Connection=True;";
builder.Services.AddDbContext<AuthDbContext>(options => options.UseSqlServer(conn));
builder.Services.AddDbContext<HmsDbContext>(options => options.UseSqlServer(conn));

// Application services
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Reporting services
builder.Services.AddScoped<HMS.API.Application.Patient.IPatientReportService, HMS.API.Application.Patient.PatientReportService>();
builder.Services.AddScoped<HMS.API.Application.Profile.IProfileReportService, HMS.API.Application.Profile.ProfileReportService>();
builder.Services.AddScoped<HMS.API.Application.Billing.IBillingReportService, HMS.API.Application.Billing.BillingReportService>();
builder.Services.AddScoped<HMS.API.Application.Pharmacy.IPharmacyReportService, HMS.API.Application.Pharmacy.PharmacyReportService>();
builder.Services.AddScoped<HMS.API.Application.Lab.ILabReportService, HMS.API.Application.Lab.LabReportService>();

// Profile service registration (uses HmsDbContext)
builder.Services.AddScoped<HMS.API.Application.Profile.IProfileService, HMS.API.Application.Profile.ProfileService>();

// Patient service
builder.Services.AddScoped<HMS.API.Application.Patient.IPatientService, HMS.API.Application.Patient.PatientService>();

// Billing service
builder.Services.AddScoped<HMS.API.Application.Billing.IBillingService, HMS.API.Application.Billing.BillingService>();

// Register PaymentService
builder.Services.AddScoped<HMS.API.Application.Payments.IPaymentService, HMS.API.Application.Payments.PaymentService>();

// Lab service
builder.Services.AddScoped<HMS.API.Application.Lab.ILabService, HMS.API.Application.Lab.LabService>();

// Pharmacy service
builder.Services.AddScoped<HMS.API.Application.Pharmacy.IPharmacyService, HMS.API.Application.Pharmacy.PharmacyService>();

// Inventory service
builder.Services.AddScoped<HMS.API.Application.Pharmacy.IInventoryService, HMS.API.Application.Pharmacy.InventoryService>();

// Current user
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Tenant subscription service
builder.Services.AddScoped<ITenantSubscriptionService, TenantSubscriptionService>();

// Billing webhook in-memory receiver
builder.Services.AddSingleton<IBillingWebhookService, InMemoryBillingWebhookService>();

// Event publisher
builder.Services.AddSingleton<IEventPublisher, HMS.API.Infrastructure.Common.EventPublisher>();

// register outbox processor
builder.Services.AddHostedService<OutboxProcessor>();

// Authentication - JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? "dev-insecure-key-change";
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});

// Authorization - permission based
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

builder.Services.AddAuthorization(options =>
{
    // dynamic policy provider handles permission: policies
});

// Middleware
// register tenant middleware before authentication so header-based tenant overrides apply
builder.Services.AddTransient<TenantMiddleware>();

//builder.Services.AddTransient<CurrentUserMiddleware>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

// Add clean Swagger setup
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "HMS.API", Version = "v1", Description = "Hospital Management System API" });
    // avoid duplicate schema id collisions
    c.CustomSchemaIds(type => type.FullName!.Replace("+", "."));

    // Add simple example responses for a few endpoints via operation filter
    c.OperationFilter<SimpleResponseExamplesFilter>();
});

// register the operation filter
builder.Services.AddTransient<SimpleResponseExamplesFilter>();

// Notification service
builder.Services.AddSingleton<HMS.API.Application.Common.INotificationService, HMS.API.Infrastructure.Common.NotificationService>();

// Add SignalR
builder.Services.AddSignalR();

// reservation cleanup
builder.Services.AddHostedService<HMS.API.Infrastructure.Pharmacy.ReservationCleanupService>();

// Cloud sync client
builder.Services.AddHttpClient<HMS.API.Application.Sync.ICloudSyncClient, HMS.API.Infrastructure.Sync.CloudSyncClient>();
builder.Services.AddScoped<HMS.API.Application.Sync.ISyncManager, HMS.API.Infrastructure.Sync.SyncManager>();
builder.Services.AddHostedService<HMS.API.Infrastructure.Sync.BackgroundSyncService>();

// Distributed cache (Redis) for reporting caches. Configure via ConnectionStrings:Redis or set REDIS__CONFIG env.
var redisConfig = builder.Configuration.GetConnectionString("Redis") ?? builder.Configuration["Redis:Configuration"];
if (!string.IsNullOrWhiteSpace(redisConfig))
{
    builder.Services.AddStackExchangeRedisCache(options => { options.Configuration = redisConfig; });
    // register aggregator service to precompute heavy aggregates
    builder.Services.AddHostedService<HMS.API.Infrastructure.Reporting.ReportingAggregatorService>();
}

// register push notifier for pushing outbox events to tenant nodes
builder.Services.AddScoped<HMS.API.Infrastructure.Sync.PushNotifier>();

var app = builder.Build();

// Apply EF migrations on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        db.Database.Migrate();

        var hdb = scope.ServiceProvider.GetRequiredService<HmsDbContext>();
        hdb.Database.Migrate();

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await SeedData.EnsureSeedDataAsync(db, hasher);
    }
    catch
    {
        // swallow migration errors in development; in production log and fail fast
    }
}

// Enable Swagger middleware
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "HMS.API v1");
    c.DocumentTitle = "HMS.API Swagger UI";
});

app.UseHttpsRedirection();

// tenant middleware must run early to set query filter context
app.UseMiddleware<TenantMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CurrentUserMiddleware>();

// enforce subscription after tenant has been resolved
app.Use(async (context, next) =>
{
    var tenantId = CurrentTenantAccessor.CurrentTenantId;
    if (tenantId.HasValue)
    {
        var subs = context.RequestServices.GetService<ITenantSubscriptionService>();
        if (subs != null)
        {
            var allowed = await subs.IsTenantAllowedAsync(tenantId.Value);
            if (!allowed)
            {
                context.Response.StatusCode = 402; // Payment Required
                await context.Response.BodyWriter.WriteAsync(System.Text.Encoding.UTF8.GetBytes("Tenant subscription inactive or past due"));
                return;
            }
        }
    }

    await next();
});

app.MapControllers();
// Map inventory controller endpoints via attribute routing
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();

// Local operation filter providing simple example responses for a subset of endpoints
public class SimpleResponseExamplesFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation == null || context == null) return;

        var actionId = context.ApiDescription.ActionDescriptor.DisplayName ?? string.Empty;

        // Provide example for billing kpi
        if (actionId.Contains("BillingController.Kpi"))
        {
            operation.Responses["200"].Content["application/json"].Example = new Microsoft.OpenApi.Any.OpenApiObject
            {
                ["totalRevenue"] = new Microsoft.OpenApi.Any.OpenApiDouble(12345.67),
                ["invoiceCount"] = new Microsoft.OpenApi.Any.OpenApiInteger(123),
                ["paidCount"] = new Microsoft.OpenApi.Any.OpenApiInteger(100),
                ["unpaidCount"] = new Microsoft.OpenApi.Any.OpenApiInteger(23),
                ["averageInvoice"] = new Microsoft.OpenApi.Any.OpenApiDouble(100.37)
            };
        }

        // Provide example for monthly revenue
        if (actionId.Contains("BillingController.Monthly"))
        {
            operation.Responses["200"].Content["application/json"].Example = new Microsoft.OpenApi.Any.OpenApiArray
            {
                new Microsoft.OpenApi.Any.OpenApiObject
                {
                    ["year"] = new Microsoft.OpenApi.Any.OpenApiInteger(DateTime.UtcNow.Year),
                    ["month"] = new Microsoft.OpenApi.Any.OpenApiInteger(DateTime.UtcNow.Month),
                    ["revenue"] = new Microsoft.OpenApi.Any.OpenApiDouble(12345.00)
                }
            };
        }
    }
}
