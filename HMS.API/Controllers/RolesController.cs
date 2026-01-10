using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Application.Auth.DTOs;
using HMS.API.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
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

        public RolesController(AuthDbContext db)
        {
            _db = db;
        }

        [HttpPost]
        [HasPermission("roles.manage")]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest model)
        {
            if (await _db.Roles.AnyAsync(r => r.Name == model.Name)) return BadRequest(new { error = "Role exists" });
            var role = new Role { Name = model.Name, Description = model.Description };
            _db.Roles.Add(role);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetRole), new { id = role.Id }, new RoleResponse { Id = role.Id, Name = role.Name, Description = role.Description });
        }

        [HttpPut("{id}")]
        [HasPermission("roles.manage")]
        public async Task<IActionResult> UpdateRole(Guid id, [FromBody] CreateRoleRequest model)
        {
            var role = await _db.Roles.SingleOrDefaultAsync(r => r.Id == id);
            if (role == null) return NotFound();

            role.Name = model.Name;
            role.Description = model.Description;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("{id}")]
        [HasPermission("roles.manage")]
        public async Task<IActionResult> GetRole(Guid id)
        {
            var role = await _db.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission).SingleOrDefaultAsync(r => r.Id == id);
            if (role == null) return NotFound();
            return Ok(new RoleResponse { Id = role.Id, Name = role.Name, Description = role.Description, Permissions = role.RolePermissions.Select(rp => rp.Permission.Code) });
        }

        [HttpPost("{roleId}/permissions")]
        [HasPermission("roles.manage")]
        public async Task<IActionResult> AddPermission(Guid roleId, [FromBody] AddPermissionRequest permission)
        {
            var role = await _db.Roles.Include(r => r.RolePermissions).SingleOrDefaultAsync(r => r.Id == roleId);
            if (role == null) return NotFound(new { error = "Role not found" });

            var existing = await _db.Permissions.SingleOrDefaultAsync(p => p.Code == permission.Code);
            if (existing == null)
            {
                existing = new Permission { Code = permission.Code, Description = permission.Description };
                _db.Permissions.Add(existing);
                await _db.SaveChangesAsync();
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
            var role = await _db.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission).SingleOrDefaultAsync(r => r.Id == roleId);
            if (role == null) return NotFound(new { error = "Role not found" });

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
            var roles = await _db.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission).ToListAsync();
            var resp = roles.Select(r => new RoleResponse { Id = r.Id, Name = r.Name, Description = r.Description, Permissions = r.RolePermissions.Select(rp => rp.Permission.Code) });
            return Ok(resp);
        }
    }
}