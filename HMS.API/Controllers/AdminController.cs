using System;
using System.Threading.Tasks;
using HMS.API.Infrastructure.Auth;
using HMS.API.Infrastructure.Persistence;
using HMS.API.Application.Common;
using HMS.API.Application.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("admin")]
    public class AdminController : ControllerBase
    {
        private readonly IServiceProvider _services;
        private readonly IHostEnvironment _env;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IServiceProvider services, IHostEnvironment env, ILogger<AdminController> logger)
        {
            _services = services;
            _env = env;
            _logger = logger;
        }

        // Temporary development-only endpoint to trigger DB seed operations
        [HttpPost("seed")]
        public async Task<IActionResult> Seed()
        {
            if (!_env.IsDevelopment())
            {
                return Forbid();
            }

            try
            {
                using var scope = _services.CreateScope();
                var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
                var hdb = scope.ServiceProvider.GetRequiredService<HmsDbContext>();
                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

                // Seed auth DB (default admin/roles)
                await SeedData.EnsureSeedDataAsync(authDb, hasher);

                // Seed HMS DB (default tenant and related data)
                await HmsSeedData.EnsureSeedDataAsync(hdb, authDb, hasher);

                return Ok(new { message = "Seed completed" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Seeding failed");
                return Problem(detail: ex.Message, statusCode: 500);
            }
        }
    }
}
