using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Application.Auth;
using HMS.API.Infrastructure.Persistence;
using HMS.API.Domain.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HMS.API.Security;
using HMS.API.Application.Auth.DTOs;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LocalUsersController : ControllerBase
    {
        private readonly LocalAuthService _localAuth;
        private readonly HmsDbContext _db;

        public LocalUsersController(LocalAuthService localAuth, HmsDbContext db)
        {
            _localAuth = localAuth;
            _db = db;
        }

        [HttpGet]
        [HasPermission("users.manage")]
        public async Task<IActionResult> List([FromQuery] Guid? tenantId = null)
        {
            var q = _db.Set<LocalUser>().AsNoTracking().Where(u => !u.IsDeleted);
            if (tenantId.HasValue) q = q.Where(u => u.TenantId == tenantId.Value);
            var users = await q.Select(u => new { u.Id, u.Username, u.Email, u.TenantId, u.IsLocked }).ToListAsync();
            return Ok(users);
        }

        [HttpPost]
        [HasPermission("users.manage")]
        public async Task<IActionResult> Create([FromBody] RegisterRequest req, [FromQuery] Guid? tenantId = null)
        {
            try
            {
                await _localAuth.RegisterLocalAsync(req, tenantId);
                return Created("/localusers", null);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("{id}/lock")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> Lock(Guid id)
        {
            var u = await _db.Set<LocalUser>().SingleOrDefaultAsync(x => x.Id == id);
            if (u == null) return NotFound();
            u.IsLocked = true;
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
