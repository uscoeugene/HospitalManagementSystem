using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Infrastructure.Auth;
using HMS.API.Infrastructure.Persistence;
using HMS.API.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HMS.API.Security;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TenantsController : ControllerBase
    {
        private readonly AuthDbContext _authDb;
        private readonly IConfiguration _config;

        public TenantsController(AuthDbContext authDb, IConfiguration config)
        {
            _authDb = authDb;
            _config = config;
        }

        [HttpGet]
        [HasPermission("users.manage")]
        public async Task<IActionResult> List()
        {
            var t = await _authDb.Tenants.AsNoTracking().ToListAsync();
            return Ok(t.Select(x => new { x.Id, x.Name, x.Code, x.IsCentral }));
        }

        [HttpPost]
        [HasPermission("users.manage")]
        public async Task<IActionResult> Create([FromBody] CreateTenantRequest req)
        {
            if (await _authDb.Tenants.AnyAsync(x => x.Code == req.Code)) return BadRequest(new { error = "Tenant code exists" });
            var t = new Tenant { Name = req.Name, Code = req.Code, IsCentral = false };
            _authDb.Tenants.Add(t);
            await _authDb.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = t.Id }, new { t.Id, t.Name, t.Code, t.IsCentral });
        }

        [HttpGet("{id}")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> Get(Guid id)
        {
            var t = await _authDb.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (t == null) return NotFound();
            return Ok(new { t.Id, t.Name, t.Code, t.IsCentral });
        }

        // Issue a signed tenant JWT (contains tenant_id) for offline nodes to use when syncing/connecting
        [HttpPost("{id}/token")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> IssueToken(Guid id)
        {
            var t = await _authDb.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (t == null) return NotFound();

            var key = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured");
            var issuer = _config["Jwt:Issuer"] ?? "hms";
            var audience = _config["Jwt:Audience"] ?? "hms_clients";

            var expires = DateTimeOffset.UtcNow.AddDays(365);

            var claims = new[] {
                new Claim("tenant_id", t.Id.ToString()),
                new Claim("token_type", "tenant_token")
            };

            var keyBytes = Encoding.UTF8.GetBytes(key);
            var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(issuer, audience, claims, expires: expires.UtcDateTime, signingCredentials: creds);
            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new { tenantId = t.Id, token = tokenString, expiresAt = expires });
        }
    }

    public class CreateTenantRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
