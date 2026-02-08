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
    [Route("[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AuthDbContext _authDb;

        public UsersController(AuthDbContext authDb)
        {
            _authDb = authDb;
        }

        [HttpGet]
        [HasPermission("users.manage")]
        public async Task<IActionResult> List()
        {
            var users = await _authDb.Users.AsNoTracking().ToListAsync();
            return Ok(users.Select(u => new { u.Id, u.Username, u.Email, u.TenantId, u.IsLocked }));
        }

        [HttpPost("{id}/assign-tenant/{tenantId}")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> AssignTenant(Guid id, Guid tenantId)
        {
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
            var user = await _authDb.Users.SingleOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound(new { error = "User not found" });

            user.TenantId = null;
            await _authDb.SaveChangesAsync();
            return NoContent();
        }
    }
}
