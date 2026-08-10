using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Application.Auth.DTOs;
using HMS.API.Domain.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HMS.API.Infrastructure.Auth;
using HMS.API.Security;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RolesController : ControllerBase
    {
        private readonly AuthDbContext _db;
        private readonly HMS.API.Application.Common.ICurrentUserService _currentUser;

        public RolesController(AuthDbContext db, HMS.API.Application.Common.ICurrentUserService currentUser)
        {
            _db = db;
            _currentUser = currentUser;
        }

        [HttpPost]
        [HasPermission("roles.manage")]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest model)
        {
            if (RoleCatalog.IsCoreRoleName(model.Name))
            {
                return BadRequest(new { error = "Built-in role names are reserved." });
            }

            if (await RolesQuery().AnyAsync(r => r.Name == model.Name)) return BadRequest(new { error = "Role exists" });
            var role = new Role { Name = model.Name, Description = model.Description };
            _db.Roles.Add(role);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetRole), new { id = role.Id }, new RoleResponse { Id = role.Id, Name = role.Name, Description = role.Description, IsSystem = RoleCatalog.IsCoreRoleName(role.Name) });
        }

        [HttpPut("{id}")]
        [HasPermission("roles.manage")]
        public async Task<IActionResult> UpdateRole(Guid id, [FromBody] CreateRoleRequest model)
        {
            var role = await RolesQuery().SingleOrDefaultAsync(r => r.Id == id);
            if (role == null) return NotFound();
            if (RoleCatalog.IsCoreRoleName(role.Name))
            {
                return BadRequest(new { error = "Built-in roles cannot be edited." });
            }
            if (RoleCatalog.IsCoreRoleName(model.Name))
            {
                return BadRequest(new { error = "Built-in role names are reserved." });
            }

            if (await RolesQuery().AnyAsync(r => r.Id != id && r.Name == model.Name)) return BadRequest(new { error = "Role exists" });

            role.Name = model.Name;
            role.Description = model.Description;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("{id}")]
        [HasPermission("roles.manage")]
        public async Task<IActionResult> GetRole(Guid id)
        {
            var role = await RolesQuery().Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission).SingleOrDefaultAsync(r => r.Id == id);
            if (role == null) return NotFound();
            return Ok(new RoleResponse { Id = role.Id, Name = role.Name, Description = role.Description, IsSystem = RoleCatalog.IsCoreRoleName(role.Name), Permissions = role.RolePermissions.Select(rp => rp.Permission.Code) });
        }

        [HttpGet("permissions")]
        [HasPermission("roles.manage")]
        public async Task<IActionResult> ListPermissions()
        {
            var permissions = await PermissionsQuery()
                .OrderBy(p => p.Code)
                .Select(p => new PermissionResponse
                {
                    Code = p.Code,
                    Description = p.Description
                })
                .ToListAsync();

            return Ok(permissions);
        }

        [HttpPost("permissions")]
        [HasPermission("system.permissions.manage")]
        public async Task<IActionResult> CreatePermission([FromBody] AddPermissionRequest permission)
        {
            var code = permission.Code?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                return BadRequest(new { error = "Permission code is required" });
            }

            var existing = await PermissionsQuery().SingleOrDefaultAsync(p => p.Code == code);
            if (existing != null)
            {
                return BadRequest(new { error = "Permission already exists" });
            }

            var created = new Permission
            {
                Code = code,
                Description = permission.Description?.Trim() ?? string.Empty
            };

            _db.Permissions.Add(created);
            await _db.SaveChangesAsync();

            return Ok(new PermissionResponse
            {
                Code = created.Code,
                Description = created.Description
            });
        }

        [HttpPost("{roleId}/permissions")]
        [HasPermission("roles.manage")]
        public async Task<IActionResult> AddPermission(Guid roleId, [FromBody] AddPermissionRequest permission)
        {
            var role = await RolesQuery().Include(r => r.RolePermissions).SingleOrDefaultAsync(r => r.Id == roleId);
            if (role == null) return NotFound(new { error = "Role not found" });
            if (RoleCatalog.IsCoreRoleName(role.Name))
            {
                return BadRequest(new { error = "Built-in roles cannot be modified." });
            }

            var code = permission.Code?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                return BadRequest(new { error = "Permission code is required" });
            }

            var existing = await PermissionsQuery().SingleOrDefaultAsync(p => p.Code == code);
            if (existing == null)
            {
                return BadRequest(new { error = "Permission not found. Use the permission catalog dropdown, or register it as a system permission first." });
            }

            if (!role.RolePermissions.Any(rp => rp.PermissionId == existing.Id))
            {
                role.RolePermissions.Add(new RolePermission { Role = role, Permission = existing });
                await _db.SaveChangesAsync();
            }

            return Ok();
        }

        [HttpDelete("{roleId}/permissions/{permissionCode}")]
        [HasPermission("roles.manage")]
        public async Task<IActionResult> RemovePermission(Guid roleId, string permissionCode)
        {
            var role = await RolesQuery().Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission).SingleOrDefaultAsync(r => r.Id == roleId);
            if (role == null) return NotFound(new { error = "Role not found" });
            if (RoleCatalog.IsCoreRoleName(role.Name))
            {
                return BadRequest(new { error = "Built-in roles cannot be modified." });
            }

            var rp = role.RolePermissions.SingleOrDefault(x => x.Permission.Code == permissionCode);
            if (rp == null) return NotFound(new { error = "Permission not found on role" });

            role.RolePermissions.Remove(rp);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet]
        [HasPermission("roles.manage")]
        public async Task<IActionResult> ListRoles()
        {
            var roles = await RolesQuery().Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission).OrderBy(r => r.Name).ToListAsync();
            var resp = roles.Select(r => new RoleResponse { Id = r.Id, Name = r.Name, Description = r.Description, IsSystem = RoleCatalog.IsCoreRoleName(r.Name), Permissions = r.RolePermissions.Select(rp => rp.Permission.Code) });
            return Ok(resp);
        }

        [HttpDelete("{id}")]
        [HasPermission("roles.manage")]
        public async Task<IActionResult> DeleteRole(Guid id)
        {
            var role = await RolesQuery()
                .Include(r => r.UserRoles)
                .Include(r => r.RolePermissions)
                .SingleOrDefaultAsync(r => r.Id == id);

            if (role == null) return NotFound();
            if (RoleCatalog.IsCoreRoleName(role.Name))
            {
                return BadRequest(new { error = "Built-in roles cannot be deleted." });
            }
            if (role.UserRoles.Any()) return BadRequest(new { error = "Role cannot be deleted while assigned to users." });

            if (role.RolePermissions.Any())
            {
                _db.RolePermissions.RemoveRange(role.RolePermissions);
            }

            _db.Roles.Remove(role);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        private IQueryable<Role> RolesQuery()
        {
            var query = _db.Roles.IgnoreQueryFilters().AsQueryable().Where(r => !r.IsDeleted);

            if (_currentUser.TenantId.HasValue)
            {
                var tenantId = _currentUser.TenantId.Value;
                query = query.Where(r => r.TenantId == null || r.TenantId == tenantId);
            }

            return query;
        }

        private IQueryable<Permission> PermissionsQuery()
        {
            var query = _db.Permissions.IgnoreQueryFilters().AsQueryable().Where(p => !p.IsDeleted);

            if (_currentUser.TenantId.HasValue)
            {
                var tenantId = _currentUser.TenantId.Value;
                query = query.Where(p => p.TenantId == null || p.TenantId == tenantId);
            }

            return query;
        }
    }
}

