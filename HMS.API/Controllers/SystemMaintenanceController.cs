using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Application.Auth;
using HMS.API.Application.Common;
using HMS.API.Domain.Auth;
using HMS.API.Infrastructure.Auth;
using HMS.API.Infrastructure.Persistence;
using HMS.API.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("system-maintenance")]
    public class SystemMaintenanceController : ControllerBase
    {
        private readonly AuthDbContext _authDb;
        private readonly HmsDbContext _hmsDb;
        private readonly IPasswordHasher _hasher;
        private readonly ICurrentUserService _currentUser;

        public SystemMaintenanceController(AuthDbContext authDb, HmsDbContext hmsDb, IPasswordHasher hasher, ICurrentUserService currentUser)
        {
            _authDb = authDb;
            _hmsDb = hmsDb;
            _hasher = hasher;
            _currentUser = currentUser;
        }

        [HttpPost("run")]
        [HasPermission("system.maintenance.manage")]
        public async Task<IActionResult> Run([FromBody] RunMaintenanceRequest request)
        {
            var scope = NormalizeScope(request.Scope);
            if (string.IsNullOrWhiteSpace(scope))
            {
                return BadRequest(new { error = "Maintenance scope is required." });
            }

            if (!string.Equals(request.Confirmation?.Trim(), "RESET", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "Confirmation text must be RESET." });
            }

            if (scope == MaintenanceScopes.TenantAuthReset)
            {
                if (!request.TenantId.HasValue)
                {
                    return BadRequest(new { error = "Tenant is required for tenant auth reset." });
                }

                var tenant = await _authDb.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.TenantId.Value && !x.IsDeleted);
                if (tenant == null)
                {
                    return NotFound(new { error = "Tenant not found." });
                }

                var expectedTenantCode = tenant.Code?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(request.TenantCodeConfirmation) ||
                    !string.Equals(request.TenantCodeConfirmation.Trim(), expectedTenantCode, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { error = "Tenant code confirmation does not match." });
                }

                await ResetTenantAuthAsync(tenant.Id);
                await SeedData.EnsureSeedDataAsync(_authDb, _hasher);
                await WriteAuditAsync("SystemMaintenance.TenantAuthReset", $"Reset tenant auth data for {tenant.Name} ({tenant.Code}). Scope={scope}.", tenant.Id);

                return Ok(new
                {
                    message = "Tenant auth data reset.",
                    scope,
                    tenantId = tenant.Id,
                    tenantCode = tenant.Code
                });
            }

            if (scope == MaintenanceScopes.AuthSeed)
            {
                await SeedData.EnsureSeedDataAsync(_authDb, _hasher);
                await WriteAuditAsync("SystemMaintenance.AuthSeed", "Refreshed seeded auth catalog and built-in roles.", null);
                return Ok(new { message = "Auth seed refreshed.", scope });
            }

            if (scope == MaintenanceScopes.PlatformSeed)
            {
                await SeedData.EnsureSeedDataAsync(_authDb, _hasher);
                await HMS.API.Infrastructure.Persistence.HmsSeedData.EnsureSeedDataAsync(_hmsDb, _authDb, _hasher);
                await WriteAuditAsync("SystemMaintenance.PlatformSeed", "Refreshed auth seed and platform seed data.", null);
                return Ok(new { message = "Platform reseed completed.", scope });
            }

            return BadRequest(new { error = "Unsupported maintenance scope." });
        }

        [HttpPost("reseed-auth")]
        [HasPermission("system.maintenance.manage")]
        public async Task<IActionResult> ReseedAuth()
        {
            await SeedData.EnsureSeedDataAsync(_authDb, _hasher);
            await WriteAuditAsync("SystemMaintenance.AuthSeed", "Refreshed seeded auth catalog and built-in roles.", null);
            return Ok(new { message = "Auth seed refreshed." });
        }

        [HttpPost("reseed-platform")]
        [HasPermission("system.maintenance.manage")]
        public async Task<IActionResult> ReseedPlatform()
        {
            await SeedData.EnsureSeedDataAsync(_authDb, _hasher);
            await HMS.API.Infrastructure.Persistence.HmsSeedData.EnsureSeedDataAsync(_hmsDb, _authDb, _hasher);
            await WriteAuditAsync("SystemMaintenance.PlatformSeed", "Refreshed auth seed and platform seed data.", null);
            return Ok(new { message = "Platform reseed completed." });
        }

        [HttpPost("tenants/{tenantId:guid}/reset-auth")]
        [HasPermission("system.maintenance.manage")]
        public async Task<IActionResult> ResetTenantAuth(Guid tenantId)
        {
            var tenant = await _authDb.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == tenantId && !x.IsDeleted);
            if (tenant == null)
            {
                return NotFound(new { error = "Tenant not found." });
            }

            await ResetTenantAuthAsync(tenantId);
            await SeedData.EnsureSeedDataAsync(_authDb, _hasher);
            await WriteAuditAsync("SystemMaintenance.TenantAuthReset", $"Reset tenant auth data for {tenant.Name} ({tenant.Code}).", tenantId);
            return Ok(new { message = "Tenant auth data reset.", tenantId });
        }

        private async Task ResetTenantAuthAsync(Guid tenantId)
        {
            var userIds = await _authDb.Users.IgnoreQueryFilters()
                .Where(u => u.TenantId == tenantId && !u.IsDeleted)
                .Select(u => u.Id)
                .ToArrayAsync();

            var roleIds = await _authDb.Roles.IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId && !r.IsDeleted)
                .Select(r => r.Id)
                .ToArrayAsync();

            if (userIds.Length > 0)
            {
                var userRoles = await _authDb.UserRoles.IgnoreQueryFilters().Where(ur => userIds.Contains(ur.UserId)).ToListAsync();
                _authDb.UserRoles.RemoveRange(userRoles);

                var refreshTokens = await _authDb.RefreshTokens.IgnoreQueryFilters().Where(rt => userIds.Contains(rt.UserId)).ToListAsync();
                _authDb.RefreshTokens.RemoveRange(refreshTokens);

                var userDepartments = await _authDb.Set<HMS.API.Domain.Auth.UserDepartment>().IgnoreQueryFilters().Where(ud => userIds.Contains(ud.UserId)).ToListAsync();
                _authDb.Set<HMS.API.Domain.Auth.UserDepartment>().RemoveRange(userDepartments);

                var users = await _authDb.Users.IgnoreQueryFilters().Where(u => u.TenantId == tenantId).ToListAsync();
                _authDb.Users.RemoveRange(users);
            }

            if (roleIds.Length > 0)
            {
                var rolePermissions = await _authDb.RolePermissions.IgnoreQueryFilters().Where(rp => roleIds.Contains(rp.RoleId)).ToListAsync();
                _authDb.RolePermissions.RemoveRange(rolePermissions);

                var roles = await _authDb.Roles.IgnoreQueryFilters().Where(r => r.TenantId == tenantId).ToListAsync();
                _authDb.Roles.RemoveRange(roles);
            }

            var tenantSubscriptions = await _authDb.Set<HMS.API.Domain.Common.TenantSubscription>().IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ToListAsync();
            _authDb.Set<HMS.API.Domain.Common.TenantSubscription>().RemoveRange(tenantSubscriptions);

            var tenantNodes = await _authDb.Set<HMS.API.Domain.Common.TenantNode>().IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ToListAsync();
            _authDb.Set<HMS.API.Domain.Common.TenantNode>().RemoveRange(tenantNodes);

            var tenantDomains = await _authDb.Set<HMS.API.Domain.Common.TenantDomain>().IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ToListAsync();
            _authDb.Set<HMS.API.Domain.Common.TenantDomain>().RemoveRange(tenantDomains);

            await _authDb.SaveChangesAsync();
        }

        private async Task WriteAuditAsync(string action, string details, Guid? tenantId)
        {
            try
            {
                _authDb.AuthAudits.Add(new AuthAudit
                {
                    UserId = _currentUser.UserId ?? Guid.Empty,
                    TenantId = tenantId,
                    Action = action,
                    Details = details,
                    PerformedAt = DateTimeOffset.UtcNow
                });

                await _authDb.SaveChangesAsync();
            }
            catch
            {
                // Maintenance should still complete even if audit persistence fails.
            }
        }

        private static string NormalizeScope(string? scope)
        {
            return (scope ?? string.Empty).Trim().ToLowerInvariant();
        }
    }

    public static class MaintenanceScopes
    {
        public const string AuthSeed = "auth-seed";
        public const string PlatformSeed = "platform-seed";
        public const string TenantAuthReset = "tenant-auth-reset";
    }

    public class RunMaintenanceRequest
    {
        public string? Scope { get; set; }
        public Guid? TenantId { get; set; }
        public string? TenantCodeConfirmation { get; set; }
        public string? Confirmation { get; set; }
    }
}
