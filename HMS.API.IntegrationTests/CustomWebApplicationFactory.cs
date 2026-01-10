using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using HMS.API.Infrastructure.Auth;
using HMS.API.Application.Auth;
using HMS.API.Application.Common;
using HMS.API.Infrastructure.Auth;

namespace HMS.API.IntegrationTests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Remove the existing AuthDbContext registration
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AuthDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                // Remove production CurrentUserService
                var curUserDesc = services.SingleOrDefault(d => d.ServiceType == typeof(ICurrentUserService));
                if (curUserDesc != null) services.Remove(curUserDesc);

                // Add in-memory database for tests
                services.AddDbContext<AuthDbContext>(options => {
                    options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid().ToString("N"));
                });

                services.AddScoped<IPasswordHasher, PasswordHasher>();
                services.AddScoped<ICurrentUserService, TestCurrentUserService>();

                // Ensure service provider builds and seed data applied per test
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                db.Database.EnsureCreated();
                SeedData.EnsureSeedDataAsync(db, hasher).GetAwaiter().GetResult();
            });
        }
    }

    // Simple test current user service that returns null (no authenticated user) or you can extend it per test
    internal class TestCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
    }
}