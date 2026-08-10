using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using HMS.API.Infrastructure.Auth;
using HMS.API.Infrastructure.Persistence;
using HMS.API.Domain.Common;
using HMS.API.Application.Common;
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

        [HttpGet("diagnostics/tenant-resolve")]
        public async Task<IActionResult> ResolveTenantDiagnostic()
        {
            // Returns detailed information about how the tenant would be resolved for this request
            var svcProvider = HttpContext.RequestServices;
            var resolver = svcProvider.GetService(typeof(ITenantResolver)) as ITenantResolver;
            var cfg = svcProvider.GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration)) as Microsoft.Extensions.Configuration.IConfiguration;

            string hostHeaderRaw = Request.Headers.ContainsKey("Host") ? Request.Headers["Host"].ToString() : string.Empty;
            string xTenantHost = Request.Headers.ContainsKey("X-Tenant-Host") ? Request.Headers["X-Tenant-Host"].ToString() : string.Empty;
            string xOriginalHost = Request.Headers.ContainsKey("X-Original-Host") ? Request.Headers["X-Original-Host"].ToString() : string.Empty;
            string xForwardedHost = Request.Headers.ContainsKey("X-Forwarded-Host") ? Request.Headers["X-Forwarded-Host"].ToString() : string.Empty;
            string xTenantIdHdr = Request.Headers.ContainsKey("X-Tenant-Id") ? Request.Headers["X-Tenant-Id"].ToString() : string.Empty;
            string xTenantCodeHdr = Request.Headers.ContainsKey("X-Tenant-Code") ? Request.Headers["X-Tenant-Code"].ToString() : string.Empty;

            string host = Request.Host.Host;
            try
            {
                // mirror middleware logic
                if (!string.IsNullOrWhiteSpace(xTenantHost)) host = xTenantHost.Split(',')[0].Trim();
                else if (!string.IsNullOrWhiteSpace(xOriginalHost)) host = xOriginalHost.Split(',')[0].Trim();
                else if (!string.IsNullOrWhiteSpace(xForwardedHost)) host = xForwardedHost.Split(',')[0].Trim();
                else if (!string.IsNullOrWhiteSpace(hostHeaderRaw)) host = hostHeaderRaw.Split(',')[0].Trim();
            }
            catch (Exception ex) { try { System.Diagnostics.Trace.TraceError(ex.ToString()); } catch { } }

            // normalize
            if (!string.IsNullOrWhiteSpace(host))
            {
                var ci = host.IndexOf(':');
                if (ci > 0) host = host.Substring(0, ci).Trim();
                host = host.Trim();
            }

            Guid? resolved = null;
            if (resolver != null)
            {
                try { resolved = await resolver.ResolveTenantIdFromHostAsync(host); } catch { /* swallow for diagnostics */ }
            }

            // middleware resolved tenant (if middleware ran earlier in pipeline)
            Guid? middleware = null;
            if (HttpContext.Items.TryGetValue("TenantId", out var tv) && tv is Guid g) middleware = g;

            // current tenant accessor
            var currentTenant = HMS.API.Application.Common.CurrentTenantAccessor.CurrentTenantId;

            // platform domain list
            var platformList = cfg?.GetSection("PlatformDomains").Get<string[]>() ?? Array.Empty<string>();
            bool isPlatformDomain = Array.Exists(platformList, d => string.Equals(d, host, StringComparison.OrdinalIgnoreCase));

            // Attempt to lookup tenant by domain and subdomain behavior for diagnostics
            object? tenantDomainRecord = null;
            try
            {
                if (resolver != null)
                {
                    // We don't have direct DB access here; attempt to call resolver internal logic if available
                    // Also attempt DNS resolution for host
                    try
                    {
                        var addrs = await Dns.GetHostAddressesAsync(host);
                        tenantDomainRecord = addrs.Select(a => a.ToString()).ToArray();
                    }
                    catch (Exception dex)
                    {
                        tenantDomainRecord = new { dnsError = dex.Message };
                    }
                }
            }
            catch (Exception ex) { try { System.Diagnostics.Trace.TraceError(ex.ToString()); } catch { } }

            var result = new
            {
                request = new
                {
                    scheme = Request.Scheme,
                    isHttps = Request.IsHttps,
                    path = Request.Path.Value,
                    hostHeaderRaw,
                    xTenantHost,
                    xOriginalHost,
                    xForwardedHost,
                    remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    xTenantIdHdr,
                    xTenantCodeHdr
                },
                normalizedHost = host,
                isPlatformDomain,
                resolvedFromResolver = resolved,
                resolvedByMiddleware = middleware,
                currentTenantAccessor = currentTenant,
                dns = tenantDomainRecord,
                notes = new {
                    message = "If resolvedByMiddleware is null but resolvedFromResolver has a value, middleware may not be running or host header may be stripped by the proxy. If both are null, ensure tenant domain record exists in DB or send X-Tenant-Code header as a workaround."
                }
            };

            return Ok(result);
        }

        [HttpPost("{id}/set-local-default")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> SetLocalDefault(Guid id)
        {
            var t = await _authDb.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (t == null) return NotFound();

            var key = "OnPremise:TenantId";
            var setting = await _authDb.Set<HMS.API.Domain.Common.AppSetting>().SingleOrDefaultAsync(s => s.Key == key);
            if (setting == null)
            {
                setting = new HMS.API.Domain.Common.AppSetting { Key = key, Value = id.ToString() };
                _authDb.Set<HMS.API.Domain.Common.AppSetting>().Add(setting);
            }
            else
            {
                setting.Value = id.ToString();
            }

            await _authDb.SaveChangesAsync();
            return Ok(new { id = id });
        }

        [HttpGet("local-default")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> GetLocalDefault()
        {
            var key = "OnPremise:TenantId";
            var setting = await _authDb.Set<HMS.API.Domain.Common.AppSetting>().AsNoTracking().SingleOrDefaultAsync(s => s.Key == key);
            if (setting == null) return Ok(new { tenantId = (Guid?)null });
            if (Guid.TryParse(setting.Value, out var gid)) return Ok(new { tenantId = gid });
            return Ok(new { tenantId = (Guid?)null });
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
