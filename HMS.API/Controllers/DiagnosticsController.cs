using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using HMS.API.Application.Common;
using HMS.API.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("diagnostics/host")]
    public class DiagnosticsController : ControllerBase
    {
        private readonly AuthDbContext _authDb;
        private readonly IHostEnvironment _env;

        public DiagnosticsController(AuthDbContext authDb, IHostEnvironment env)
        {
            _authDb = authDb;
            _env = env;
        }

        // GET /diagnostics/host
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                var headers = Request.Headers.ToDictionary(h => h.Key, h => (string)h.Value.ToString());
                var host = Request.Host.Value ?? string.Empty;
                headers.TryGetValue("X-Tenant-Host", out var xtenantHost);
                headers.TryGetValue("X-Forwarded-Host", out var xfwd);
                headers.TryGetValue("X-Original-Host", out var xorig);
                headers.TryGetValue("X-Host", out var xhost);

                var resolvedTenantId = CurrentTenantAccessor.CurrentTenantId;
                string? tenantName = null;
                if (resolvedTenantId.HasValue)
                {
                    var t = await _authDb.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == resolvedTenantId.Value);
                    if (t != null) tenantName = t.Name;
                }

                return Ok(new
                {
                    environment = _env.EnvironmentName,
                    contentRoot = _env.ContentRootPath,
                    host,
                    xTenantHost = xtenantHost,
                    xForwardedHost = xfwd,
                    xOriginalHost = xorig,
                    xHost = xhost,
                    headers,
                    resolvedTenantId,
                    tenantName
                });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
