using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Infrastructure.Auth;
using HMS.API.Domain.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HMS.API.Security;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("auth/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AuthDbContext _authDb;
        private readonly HMS.API.Application.Common.ICurrentUserService _currentUser;

        public UsersController(AuthDbContext authDb, HMS.API.Application.Common.ICurrentUserService currentUser)
        {
            _authDb = authDb;
            _currentUser = currentUser;
        }

        [HttpGet]
        [HasPermission("users.manage")]
        public async Task<IActionResult> List()
        {
            // If caller is tenant-scoped, only return users for that tenant
            if (_currentUser.TenantId.HasValue)
            {
                var tid = _currentUser.TenantId.Value;
                var users = await _authDb.Users.AsNoTracking().Where(u => u.TenantId == tid).ToListAsync();
                return Ok(users.Select(u => new { u.Id, u.Username, u.Email, u.TenantId, u.IsLocked }));
            }

            // Central/super-admin: return all users
            var all = await _authDb.Users.AsNoTracking().ToListAsync();
            return Ok(all.Select(u => new { u.Id, u.Username, u.Email, u.TenantId, u.IsLocked }));
        }

        [HttpPost("{id}/assign-tenant/{tenantId}")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> AssignTenant(Guid id, Guid tenantId)
        {
            // Only central/super-admin context may assign arbitrary tenants.
            if (_currentUser.TenantId.HasValue) return Forbid();

            var user = await _authDb.Users.SingleOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound(new { error = "User not found" });

            var tenant = await _authDb.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId);
            if (tenant == null) return NotFound(new { error = "Tenant not found" });

            user.TenantId = tenantId;
            await _authDb.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("{id}/clear-tenant")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> ClearTenant(Guid id)
        {
            // Only central/super-admin context may clear tenant assignments
            if (_currentUser.TenantId.HasValue) return Forbid();

            var user = await _authDb.Users.SingleOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound(new { error = "User not found" });

            user.TenantId = null;
            await _authDb.SaveChangesAsync();
            return NoContent();
        }

        // GET auth/users/{id}/roles
        [HttpGet("{id}/roles")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> GetUserRoles(Guid id)
        {
            // Ensure requesting tenant can only inspect roles for users within same tenant
            if (_currentUser.TenantId.HasValue)
            {
                var target = await _authDb.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);
                if (target == null) return NotFound(new { error = "User not found" });
                if (target.TenantId != _currentUser.TenantId) return Forbid();
            }

            var roles = await _authDb.UserRoles.AsNoTracking().Where(ur => ur.UserId == id).Select(ur => ur.RoleId).ToArrayAsync();
            return Ok(roles);
        }

        // POST auth/users/{id}/roles/{roleId}
        [HttpPost("{id}/roles/{roleId}")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> AssignRole(Guid id, Guid roleId)
        {
            var user = await _authDb.Users.SingleOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound(new { error = "User not found" });

            // Tenant-scoped users cannot be modified by other tenants
            if (_currentUser.TenantId.HasValue && user.TenantId != _currentUser.TenantId) return Forbid();

            var role = await _authDb.Roles.SingleOrDefaultAsync(r => r.Id == roleId);
            if (role == null) return NotFound(new { error = "Role not found" });

            var exists = await _authDb.UserRoles.AnyAsync(ur => ur.UserId == id && ur.RoleId == roleId);
            if (!exists)
            {
                _authDb.UserRoles.Add(new Domain.Auth.UserRole { UserId = id, RoleId = roleId });
                await _authDb.SaveChangesAsync();
            }

            return NoContent();
        }

        // DELETE auth/users/{id}/roles/{roleId}
        [HttpDelete("{id}/roles/{roleId}")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> RemoveRole(Guid id, Guid roleId)
        {
            var ur = await _authDb.UserRoles.SingleOrDefaultAsync(x => x.UserId == id && x.RoleId == roleId);
            if (ur == null) return NotFound(new { error = "Role assignment not found" });

            var user = await _authDb.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound(new { error = "User not found" });
            if (_currentUser.TenantId.HasValue && user.TenantId != _currentUser.TenantId) return Forbid();

            _authDb.UserRoles.Remove(ur);
            await _authDb.SaveChangesAsync();
            return NoContent();
        }

        // GET auth/users/{id}
        [HttpGet("{id}")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> Get(Guid id)
        {
            // Tenant-scoped callers may only view users in their tenant
            var user = await _authDb.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            if (_currentUser.TenantId.HasValue && user.TenantId != _currentUser.TenantId) return Forbid();

            return Ok(new { user.Id, user.Username, user.Email, user.TenantId, user.IsLocked });
        }
    }
}
