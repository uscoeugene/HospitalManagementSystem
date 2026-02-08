using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HMS.API.Infrastructure.Auth;
using HMS.API.Application.Auth;
using HMS.API.Application.Common;
using HMS.API.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Text;

namespace HMS.API.IntegrationTests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // inject test configuration for webhook secret
            builder.ConfigureAppConfiguration((ctx, conf) =>
            {
                var dict = new Dictionary<string, string>
                {
                    // base64 of UTF8 bytes of "test-secret"
                    ["Billing:WebhookSecret"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("test-secret"))
                };
                conf.AddInMemoryCollection(dict);
            });

            builder.ConfigureServices(services =>
            {
                // Remove existing DbContext registrations so we can replace with in-memory test DBs

                var authDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AuthDbContext>));
                if (authDescriptor != null) services.Remove(authDescriptor);

                var hmsDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<HmsDbContext>));
                if (hmsDescriptor != null) services.Remove(hmsDescriptor);

                // Remove production CurrentUserService
                var curUserDesc = services.SingleOrDefault(d => d.ServiceType == typeof(ICurrentUserService));
                if (curUserDesc != null) services.Remove(curUserDesc);

                // Add in-memory database for AuthDbContext
                services.AddDbContext<AuthDbContext>(options =>
                {
                    options.UseInMemoryDatabase("AuthTestDb_" + Guid.NewGuid().ToString("N"));
                });

                // Add in-memory database for HmsDbContext (profiles are part of this DB now)
                services.AddDbContext<HmsDbContext>(options =>
                {
                    options.UseInMemoryDatabase("HmsTestDb_" + Guid.NewGuid().ToString("N"));
                });

                services.AddScoped<IPasswordHasher, PasswordHasher>();

                // register a configurable test current user service so tests can set UserId and TenantId
                services.AddScoped<TestCurrentUserService>();
                services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<TestCurrentUserService>());

                // Ensure service provider builds and seed data applied per test
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();

                // Seed AuthDbContext
                var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                authDb.Database.EnsureCreated();
                SeedData.EnsureSeedDataAsync(authDb, hasher).GetAwaiter().GetResult();

                // Ensure HmsDbContext created (no specific seed required for most tests)
                var hmsDb = scope.ServiceProvider.GetRequiredService<HmsDbContext>();
                hmsDb.Database.EnsureCreated();
            });
        }
    }

    // Simple test current user service that can be configured by tests
    public class TestCurrentUserService : ICurrentUserService
    {
        // tests can set these fields directly
        public Guid? TestUserId { get; set; }
        public Guid? TestTenantId { get; set; }

        public Guid? UserId => TestUserId;
        public Guid? TenantId => TestTenantId;

        public bool HasPermission(string permission) => true; // make tests simpler; override per-test if needed
    }
}