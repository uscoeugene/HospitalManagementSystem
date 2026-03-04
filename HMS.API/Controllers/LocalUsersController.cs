using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Application.Auth;
using HMS.API.Infrastructure.Auth;
using HMS.API.Domain.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HMS.API.Security;
using HMS.API.Application.Auth.DTOs;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("users")]
    public class UsersControllerLocal : ControllerBase
    {
        private readonly LocalAuthService _localAuth;
        private readonly AuthDbContext _db;
        private readonly IPasswordHasher _hasher;

        public UsersControllerLocal(LocalAuthService localAuth, AuthDbContext db, IPasswordHasher hasher)
        {
            _localAuth = localAuth;
            _db = db;
            _hasher = hasher;
        }

        [HttpGet]
        [HasPermission("users.manage")]
        public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] Guid? tenantId = null)
        {
            var q = _db.Set<LocalUser>().AsNoTracking().Where(u => !u.IsDeleted);
            if (tenantId.HasValue) q = q.Where(u => u.TenantId == tenantId.Value);
            if (!string.IsNullOrWhiteSpace(search)) q = q.Where(u => u.Username.Contains(search) || (u.Email != null && u.Email.Contains(search)));

            var total = await q.CountAsync();
            var items = await q.OrderBy(u => u.Username).Skip((page - 1) * pageSize).Take(pageSize).Select(u => new { u.Id, u.Username, u.Email, u.TenantId, u.IsLocked }).ToListAsync();

            return Ok(new { total, page, pageSize, items });
        }

        [HttpGet("{id}")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> Get(Guid id)
        {
            var u = await _db.Set<LocalUser>().AsNoTracking().Where(x => !x.IsDeleted).Select(x => new { x.Id, x.Username, x.Email, x.TenantId, x.IsLocked }).SingleOrDefaultAsync(x => x.Id == id);
            if (u == null) return NotFound();
            return Ok(u);
        }

        [HttpPost]
        [HasPermission("users.manage")]
        public async Task<IActionResult> Create([FromBody] RegisterRequest req, [FromQuery] Guid? tenantId = null)
        {
            try
            {
                await _localAuth.RegisterLocalAsync(req, tenantId);
                return Created("/users", null);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> Update(Guid id, [FromBody] RegisterRequest req)
        {
            var u = await _db.Set<LocalUser>().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (u == null) return NotFound();
            if (!string.IsNullOrWhiteSpace(req.Password))
            {
                u.PasswordHash = _hasher.Hash(req.Password);
            }
            if (!string.IsNullOrWhiteSpace(req.Email))
            {
                u.Email = req.Email;
            }
            await _db.SaveChangesAsync();
            return NoContent();
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

        // Support browser form POST for lock
        [HttpPost("{id}/lock")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> LockPost(Guid id)
        {
            return await Lock(id);
        }

        [HttpPut("{id}/unlock")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> Unlock(Guid id)
        {
            var u = await _db.Set<LocalUser>().SingleOrDefaultAsync(x => x.Id == id);
            if (u == null) return NotFound();
            u.IsLocked = false;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // Support browser form POST for unlock
        [HttpPost("{id}/unlock")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> UnlockPost(Guid id)
        {
            return await Unlock(id);
        }

        [HttpDelete("{id}")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var u = await _db.Set<LocalUser>().SingleOrDefaultAsync(x => x.Id == id);
            if (u == null) return NotFound();
            u.IsDeleted = true;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // Support browser form POST for delete
        [HttpPost("{id}/delete")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> DeletePost(Guid id)
        {
            return await Delete(id);
        }

        [HttpPut("{id}/lock")] // duplicate route removed - kept for compatibility
        [ApiExplorerSettings(IgnoreApi = true)]
        public Task<IActionResult> Ignore() => Task.FromResult((IActionResult)NoContent());
    }
}
