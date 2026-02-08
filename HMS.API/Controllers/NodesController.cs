using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Infrastructure.Auth;
using HMS.API.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HMS.API.Security;
using System.Security.Cryptography;
using System.Text;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class NodesController : ControllerBase
    {
        private readonly AuthDbContext _authDb;

        public NodesController(AuthDbContext authDb)
        {
            _authDb = authDb;
        }

        [HttpGet("{tenantId}")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> List(Guid tenantId)
        {
            var nodes = await _authDb.Set<TenantNode>().AsNoTracking().Where(n => n.TenantId == tenantId).ToListAsync();
            return Ok(nodes.Select(n => new { n.Id, n.Name, n.CallbackUrl, n.IsActive, n.RegisteredAt }));
        }

        [HttpPost("{tenantId}")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> Register(Guid tenantId, [FromBody] RegisterNodeRequest req)
        {
            var secret = req.CallbackSecret;
            if (string.IsNullOrWhiteSpace(secret))
            {
                // generate random 32-byte secret and return base64
                var bytes = new byte[32];
                using var rng = RandomNumberGenerator.Create();
                rng.GetBytes(bytes);
                secret = Convert.ToBase64String(bytes);
            }

            var node = new TenantNode { TenantId = tenantId, CallbackUrl = req.CallbackUrl, Name = req.Name, CallbackSecret = secret };
            _authDb.Set<TenantNode>().Add(node);
            await _authDb.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = node.Id }, new { node.Id, node.CallbackUrl, node.Name, node.RegisteredAt, Secret = secret });
        }

        [HttpGet("node/{id}")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> Get(Guid id)
        {
            var n = await _authDb.Set<TenantNode>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (n == null) return NotFound();
            return Ok(new { n.Id, n.CallbackUrl, n.Name, n.IsActive, n.RegisteredAt });
        }

        [HttpPost("node/{id}/deactivate")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            var n = await _authDb.Set<TenantNode>().SingleOrDefaultAsync(x => x.Id == id);
            if (n == null) return NotFound();
            n.IsActive = false;
            await _authDb.SaveChangesAsync();
            return NoContent();
        }
    }

    public class RegisterNodeRequest
    {
        public string CallbackUrl { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? CallbackSecret { get; set; }
    }
}
