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

namespace HMS.API.IntegrationTests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
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
                services.AddScoped<ICurrentUserService, TestCurrentUserService>();

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

    // Simple test current user service that returns null (no authenticated user) or you can extend it per test
    internal class TestCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
    }
}