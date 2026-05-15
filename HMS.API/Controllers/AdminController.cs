using HMS.API.Application.Auth;
using HMS.API.Application.Common;
using HMS.API.Infrastructure.Auth;
using HMS.API.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("admin")]
    public class AdminController : ControllerBase
    {
        private readonly IServiceProvider _services;
        private readonly IHostEnvironment _env;
        private readonly ILogger<AdminController> _logger;
        private readonly IAppSettingsService _appSettings;

        public AdminController(IServiceProvider services, IHostEnvironment env, ILogger<AdminController> logger, IAppSettingsService appSettings)
        {
            _services = services;
            _env = env;
            _logger = logger;
            _appSettings = appSettings;
        }

    public class InvalidateRequest { public string Key { get; set; } = string.Empty; }

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

        [HttpPost("appsettings/invalidate")]
        public async Task<IActionResult> Invalidate([FromBody] InvalidateRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Key)) return BadRequest(new { error = "Key required" });
            await _appSettings.InvalidateAsync(req.Key);
            return Ok();
        }

        [HttpGet("health/appsettings")]
        public async Task<IActionResult> AppSettingsHealth()
        {
            try
            {
                var v = await _appSettings.GetAsync("System:DeploymentMode");
                return Ok(new { status = "ok", deploymentMode = v });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AppSettings health check failed");
                return StatusCode(503, new { status = "unavailable" });
            }
        }

        [HttpPost("reset")]
            public async Task<IActionResult> ResetDatabase([FromQuery] string key) 
        {

if (key != "wipe")
            {
                return Unauthorized();
            }
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

                _logger.LogWarning("Starting full database reset...");

                // Wipe databases
                await authDb.Database.EnsureDeletedAsync();
                await hdb.Database.EnsureDeletedAsync();

                // Recreate + apply migrations
                await authDb.Database.MigrateAsync();
                await hdb.Database.MigrateAsync();

                // Reseed
                await SeedData.EnsureSeedDataAsync(authDb, hasher);
                await HmsSeedData.EnsureSeedDataAsync(hdb, authDb, hasher);

                _logger.LogWarning("Database reset completed successfully");

                return Ok(new
                {
                    message = "Database wiped and reseeded successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database reset failed");

                return Problem(
                    detail: ex.Message,
                    statusCode: 500
                );
            }
        }
    }

}
