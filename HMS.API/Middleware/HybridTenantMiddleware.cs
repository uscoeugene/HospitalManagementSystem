using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using HMS.API.Application.Common;
using System;

namespace HMS.API.Middleware
{
    public class HybridTenantMiddleware
    {
        private readonly RequestDelegate _next;

        public HybridTenantMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Resolve scoped services from the request scope to avoid resolving them from the root provider
                var modeResolver = context.RequestServices.GetService(typeof(IDeploymentModeResolver)) as IDeploymentModeResolver;
                var tenantResolver = context.RequestServices.GetService(typeof(ITenantResolver)) as ITenantResolver;
                var logger = context.RequestServices.GetService(typeof(Microsoft.Extensions.Logging.ILogger<HybridTenantMiddleware>)) as Microsoft.Extensions.Logging.ILogger;

                var mode = DeploymentMode.Online;
                if (modeResolver != null)
                {
                    mode = await modeResolver.GetModeAsync();
                }

                logger?.LogDebug("HybridTenantMiddleware resolving mode={mode}", mode);

                if (mode == DeploymentMode.Online)
                {
                    // In online mode, tenant should come from JWT or header; middleware only attaches if present
                    if (context.User?.Identity?.IsAuthenticated == true)
                    {
                        var cid = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                        var tidClaim = context.User.FindFirst("tenant_id")?.Value;
                        if (Guid.TryParse(tidClaim, out var tid)) context.Items["TenantId"] = tid;
                    }

                    // also check header override
                    if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerTid))
                    {
                        if (Guid.TryParse(headerTid, out var ht)) context.Items["TenantId"] = ht;
                    }

                    logger?.LogDebug("Online mode: tenant_id from claims/header => {tid}", context.Items.ContainsKey("TenantId") ? context.Items["TenantId"] : null);
                }
                else
                {
                    // OnPrem mode: resolve tenant via resolver (DB primary, config fallback)
                    if (tenantResolver != null)
                    {
                        var tid = await tenantResolver.ResolveTenantIdAsync();
                        if (tid.HasValue) context.Items["TenantId"] = tid.Value;
                        logger?.LogDebug("OnPrem mode: resolved tenant => {tid}", tid);
                    }
                }
            }
            catch
            {
                // never break pipeline; leave TenantId absent if resolution fails
            }

            await _next(context);
        }
    }
}
