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

// Current user
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

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
//builder.Services.AddTransient<CurrentUserMiddleware>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CurrentUserMiddleware>();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();

// Note: RabbitMQ URL can be set via configuration key RabbitMq:Url or env RABBITMQ__URL
