using HMS.API.Application.Auth;
using HMS.API.Application.Common;
using HMS.API.Hubs;
using HMS.API.Infrastructure.Auth;
using HMS.API.Infrastructure.Logging;
using HMS.API.Infrastructure.Outbox;
using HMS.API.Infrastructure.Persistence;
using HMS.API.Middleware;
using HMS.API.Security;
using Serilog;
using Serilog.Sinks.MSSqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

// Configure Serilog early so startup logs go to configured sinks
try
{
    var cfg = builder.Configuration;
    var defaultLogDir = System.IO.Path.Combine(builder.Environment.ContentRootPath, "logs");
    System.IO.Directory.CreateDirectory(defaultLogDir);

    var connStr = cfg.GetConnectionString("Default");

    var loggerConfig = new LoggerConfiguration()
        .Enrich.FromLogContext()
        .WriteTo.File(System.IO.Path.Combine(defaultLogDir, "hms-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14);

    // If DB connection available, attempt to enable MSSQL sink (auto-create table when possible)
    if (!string.IsNullOrWhiteSpace(connStr))
    {
        try
        {
            // test connectivity
            using var test = new Microsoft.Data.SqlClient.SqlConnection(connStr);
            test.Open();

            var sinkOpts = new Serilog.Sinks.MSSqlServer.MSSqlServerSinkOptions { TableName = "Logs", AutoCreateSqlTable = true };
            var columnOptions = new Serilog.Sinks.MSSqlServer.ColumnOptions();
            // remove LogEvent column to avoid storing large payloads; keep defaults otherwise
            try { columnOptions.Store.Remove(Serilog.Sinks.MSSqlServer.StandardColumn.LogEvent); } catch { }

            loggerConfig = loggerConfig.WriteTo.MSSqlServer(connStr, sinkOpts, columnOptions: columnOptions);
        }
        catch (Exception ex)
        {
            File.WriteAllText(
                Path.Combine(builder.Environment.ContentRootPath,
                "serilog-startup-error.txt"),
                ex.ToString());

            throw;
        }
    }

    Log.Logger = loggerConfig.CreateLogger();
    builder.Host.UseSerilog();
}
catch (Exception ex)
{
    // fallback: ensure a simple file-based logger
    try { Serilog.Log.Logger = new LoggerConfiguration().WriteTo.File("logs/fallback.log").CreateLogger(); } catch { }
}


// Configure Kestrel explicitly so the app can start even if HTTPS dev cert is missing.
// We attempt to bind both HTTP and HTTPS; if HTTPS bind fails we fall back to HTTP only.
builder.WebHost.ConfigureKestrel((context, options) =>
{
    var cfg = context.Configuration;
    var httpPort = cfg.GetValue<int?>("Kestrel:Endpoints:Http:Port") ?? 5000;
    var httpsPort = cfg.GetValue<int?>("Kestrel:Endpoints:Https:Port") ?? 7142;

    // Always bind HTTP
    try
    {
        options.ListenAnyIP(httpPort);
    }
    catch (Exception ex)
    {
        // Do not fail startup if HTTP port binding is not permitted (hosted environments like Plesk/IIS)
        Console.Error.WriteLine($"Warning: Failed to bind HTTP endpoint on port {httpPort}, continuing without explicit HTTP bind. Reason: {ex.Message}");
    }

    // Try bind HTTPS using default certificate (development cert) if available.
    try
    {
        options.ListenAnyIP(httpsPort, listenOptions =>
        {
            // Use developer certificate if present; this will throw if no cert and no default available
            listenOptions.UseHttps();
            // indicate HTTPS is enabled if the hosting helper exists
            try
            {
                HMS.API.Infrastructure.Hosting.ServerSettings.HttpsEnabled = true;
            }
            catch
            {
                // ignore if ServerSettings not present or setter fails
            }
        });
    }
    catch (Exception ex)
    {
        // Swallow so app can still start on HTTP; log to console so development startup doesn't fail
        Console.Error.WriteLine($"Warning: Failed to bind HTTPS endpoint, falling back to HTTP only. Reason: {ex.Message}");
    }
});

// Register IHttpContextAccessor early so services that depend on it (CurrentUserService, DbContexts) can be constructed
builder.Services.AddHttpContextAccessor();

// Add services to the container.
builder.Services.AddControllers();
// Configure JSON options to support DateOnly serialization globally
builder.Services.AddControllers().AddJsonOptions(opts =>
{
    opts.JsonSerializerOptions.Converters.Add(new HMS.API.Json.DateOnlyJsonConverter());
    opts.JsonSerializerOptions.Converters.Add(new HMS.API.Json.NullableDateOnlyJsonConverter());
    opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

// Configure DbContexts
var conn = builder.Configuration.GetConnectionString("Default") ?? "Server=(localdb)\\MSSQLLocalDB;Database=HmsDb;Trusted_Connection=True;";
// Use distinct migrations history tables for each DbContext when they share the same database to avoid conflicts
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(conn, sqlOptions =>
    {
        sqlOptions.MigrationsHistoryTable("__AuthMigrationsHistory");
        // Enable transient error resiliency (retries) to handle transient SQL connectivity issues in production
        //sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
    }));
builder.Services.AddDbContext<HmsDbContext>(options =>
    options.UseSqlServer(conn, sqlOptions =>
    {
        sqlOptions.MigrationsHistoryTable("__HmsMigrationsHistory");
        // Enable transient error resiliency (retries) to handle transient SQL connectivity issues in production
        //sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
    }));


// Application services
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();

// Local auth/token services removed - single source of truth enforced

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
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
// Tenant provider - single source of tenant metadata for controllers/services
builder.Services.AddScoped<ITenantProvider, HMS.API.Application.Common.TenantProvider>();

// App settings and tenancy services
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IAppSettingsService, HMS.API.Application.Common.AppSettingsService>();
builder.Services.AddScoped<IDeploymentModeResolver, HMS.API.Application.Common.DeploymentModeResolver>();
builder.Services.AddScoped<ITenantResolver, HMS.API.Application.Common.TenantResolver>();

// Tenant subscription service
builder.Services.AddScoped<ITenantSubscriptionService, TenantSubscriptionService>();

// Billing webhook in-memory receiver
builder.Services.AddSingleton<IBillingWebhookService, InMemoryBillingWebhookService>();

// Event publisher
builder.Services.AddSingleton<IEventPublisher, HMS.API.Infrastructure.Common.EventPublisher>();

// Background jobs toggle (can be disabled via config or env var BackgroundJobs__Enabled=false)
// By default disable background jobs to avoid busy polling during bootstrap. Set BackgroundJobs:Enabled=true to re-enable.
var bgJobsEnabled = builder.Configuration.GetValue<bool?>("BackgroundJobs:Enabled") ?? false;
if (bgJobsEnabled)
{
    // register outbox processor
    builder.Services.AddHostedService<OutboxProcessor>();
}
else
{
    // Log a warning at startup that background jobs are disabled (can't log here — will at runtime)
}

// Authentication - JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? "dev-insecure-key-change";
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
if (keyBytes.Length < 32)
{
    using var sha = System.Security.Cryptography.SHA256.Create();
    keyBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(jwtKey));
}

// Local JWT settings for offline-issued tokens
var localKey = builder.Configuration["LocalJwt:Key"] ?? "dev-local-key-change";
var localKeyBytes = Encoding.UTF8.GetBytes(localKey);
if (localKeyBytes.Length < 32)
{
    using var sha = System.Security.Cryptography.SHA256.Create();
    localKeyBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(localKey));
}

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
        // Use resolver to accept tokens signed with either central or local key
        IssuerSigningKeyResolver = (token, securityToken, kid, parameters) => new[] { new SymmetricSecurityKey(keyBytes), new SymmetricSecurityKey(localKeyBytes) },
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    // Read JWT from cookie named HmsAuth when present (enables cookie-based auth for browser)
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            if (string.IsNullOrEmpty(ctx.Token))
            {
                if (ctx.Request.Cookies.TryGetValue("HmsAuth", out var cookieToken) && !string.IsNullOrWhiteSpace(cookieToken))
                {
                    ctx.Token = cookieToken;
                }
            }
            return Task.CompletedTask;
        }
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
// Do NOT register TenantMiddleware in DI — use app.UseMiddleware<TenantMiddleware>() at runtime. Registering middleware that requires RequestDelegate causes design-time service resolution errors for EF tools.

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
    // Add JWT bearer security definition so Swagger UI can accept tokens
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] { }
        }
    });
    // Add optional tenant header parameters via operation filter
    c.OperationFilter<HMS.API.Swagger.SwaggerAuthOperationFilter>();
});

// register the operation filter
builder.Services.AddTransient<SimpleResponseExamplesFilter>();

// Notification service
// Ensure IHttpClientFactory is available for services that depend on it
builder.Services.AddHttpClient();
builder.Services.AddSingleton<HMS.API.Application.Common.INotificationService, HMS.API.Infrastructure.Common.NotificationService>();

// register LogReadService for UI/diagnostics
builder.Services.AddSingleton<HMS.API.Infrastructure.Logging.LogReadService>();

// Add SignalR
builder.Services.AddSignalR();

// reservation cleanup and sync hosted services registered conditionally
if (bgJobsEnabled)
{
    builder.Services.AddHostedService<HMS.API.Infrastructure.Pharmacy.ReservationCleanupService>();
    builder.Services.AddHttpClient<HMS.API.Application.Sync.ICloudSyncClient, HMS.API.Infrastructure.Sync.CloudSyncClient>();
    builder.Services.AddScoped<HMS.API.Application.Sync.ISyncManager, HMS.API.Infrastructure.Sync.SyncManager>();
    builder.Services.AddHostedService<HMS.API.Infrastructure.Sync.BackgroundSyncService>();
}

// Distributed cache (Redis) for reporting caches. Configure via ConnectionStrings:Redis or set REDIS__CONFIG env.
var redisConfig = builder.Configuration.GetConnectionString("Redis") ?? builder.Configuration["Redis:Configuration"];
if (!string.IsNullOrWhiteSpace(redisConfig))
{
    builder.Services.AddStackExchangeRedisCache(options => { options.Configuration = redisConfig; });
    // register aggregator service to precompute heavy aggregates
    if (bgJobsEnabled)
    {
        builder.Services.AddHostedService<HMS.API.Infrastructure.Reporting.ReportingAggregatorService>();
    }
}

// register push notifier for pushing outbox events to tenant nodes
builder.Services.AddScoped<HMS.API.Infrastructure.Sync.PushNotifier>();

var app = builder.Build();

// Apply EF migrations on startup
//using (var scope = app.Services.CreateScope())
//{
//    try
//    {
//        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
//        db.Database.Migrate();

//        var hdb = scope.ServiceProvider.GetRequiredService<HmsDbContext>();
//        hdb.Database.Migrate();

//        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
//        await SeedData.EnsureSeedDataAsync(db, hasher);

//        // ensure HmsDb is seeded based on AuthDb
//        await HMS.API.Infrastructure.Persistence.HmsSeedData.EnsureSeedDataAsync(hdb, db, hasher);
//    }
//    catch (Exception)
//    {
//        // swallow migration errors in development; in production log and fail fast
//    }
//}

using (var scope = app.Services.CreateScope())
{
    try
    {
        // Allow disabling automatic migrations in production via configuration
        var applyMigrations = app.Configuration.GetValue<bool?>("ApplyMigrationsOnStartup") ?? true;
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var hdb = scope.ServiceProvider.GetRequiredService<HmsDbContext>();

        if (applyMigrations)
        {
            db.Database.Migrate();
            hdb.Database.Migrate();
            Log.Logger?.Information("Database migrations applied on startup");
        }
        else
        {
            Log.Logger?.Information("Skipping database migrations on startup (ApplyMigrationsOnStartup=false)");
        }

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await SeedData.EnsureSeedDataAsync(db, hasher);

        await HMS.API.Infrastructure.Persistence.HmsSeedData
            .EnsureSeedDataAsync(hdb, db, hasher);
    }
    catch (Exception ex)
    {
        // Do not crash the entire process in production for migration issues.
        // Log the full exception and rethrow only in Development so startup fails fast there.
        try
        {
            Log.Logger?.Error(ex, "Database migration or seed failed during startup");
        }
        catch { }

        if (app.Environment.IsDevelopment())
        {
            // In development we want to see and fail fast so issues are addressed early
            Console.WriteLine(ex.ToString());
            throw;
        }

        // In non-development environments log and continue so the host/shutdown process can surface
    }
}

// Enable Swagger middleware
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "HMS.API v1");
    c.DocumentTitle = "HMS.API Swagger UI";
});

//app.UseHttpsRedirection();
app.UseStaticFiles();


var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto
};
// Optionally add known proxies/networks or clear them if your proxy IPs vary:
// forwardedOptions.KnownProxies.Add(IPAddress.Parse("1.2.3.4"));
app.UseForwardedHeaders(forwardedOptions);

// then early: app.UseMiddleware<HybridTenantMiddleware>();
// tenant middleware must run early to set query filter context
// Insert request diagnostics (logs incoming headers/body samples and unsuccessful responses)
app.UseMiddleware<HMS.API.Middleware.RequestDiagnosticsMiddleware>();

if (!app.Environment.IsEnvironment("Migration"))
{
    // Use hybrid tenant middleware to determine tenant dynamically
    app.UseMiddleware<HybridTenantMiddleware>();
}

// Global API response middleware to wrap exceptions into unified response format
app.UseMiddleware<HMS.API.Middleware.ApiResponseMiddleware>();
// Wrap successful responses into standard ApiResponse<T> format
app.UseMiddleware<HMS.API.Middleware.ApiResponseWrappingMiddleware>();


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

        // Ensure unified ApiResponse<T> examples are shown for JSON responses when no explicit example exists
        try
        {
            foreach (var kv in operation.Responses)
            {
                var statusCode = kv.Key;
                var resp = kv.Value;
                if (resp.Content == null || !resp.Content.ContainsKey("application/json")) continue;

                var media = resp.Content["application/json"];
                if (media.Example != null) continue;

                if (int.TryParse(statusCode, out var code))
                {
                    if (code >= 200 && code < 300)
                    {
                        var ex = new Microsoft.OpenApi.Any.OpenApiObject
                        {
                            ["success"] = new Microsoft.OpenApi.Any.OpenApiBoolean(true),
                            ["status"] = new Microsoft.OpenApi.Any.OpenApiInteger(code),
                            ["data"] = new Microsoft.OpenApi.Any.OpenApiObject
                            {
                                ["message"] = new Microsoft.OpenApi.Any.OpenApiString("Replace with response data")
                            }
                        };
                        media.Example = ex;
                    }
                    else
                    {
                        var ex = new Microsoft.OpenApi.Any.OpenApiObject
                        {
                            ["success"] = new Microsoft.OpenApi.Any.OpenApiBoolean(false),
                            ["status"] = new Microsoft.OpenApi.Any.OpenApiInteger(code),
                            ["error"] = new Microsoft.OpenApi.Any.OpenApiObject
                            {
                                ["code"] = new Microsoft.OpenApi.Any.OpenApiString("ERROR_CODE"),
                                ["message"] = new Microsoft.OpenApi.Any.OpenApiString("Replace with error message")
                            }
                        };
                        media.Example = ex;
                    }
                }
            }
        }
        catch
        {
            // ignore example errors
        }
    }
}
