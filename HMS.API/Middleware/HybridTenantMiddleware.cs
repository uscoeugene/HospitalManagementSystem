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
                var modeResolver = context.RequestServices.GetService(typeof(IDeploymentModeResolver)) as IDeploymentModeResolver;
                var tenantResolver = context.RequestServices.GetService(typeof(ITenantResolver)) as ITenantResolver;
                var logger = context.RequestServices.GetService(typeof(Microsoft.Extensions.Logging.ILogger<HybridTenantMiddleware>)) as Microsoft.Extensions.Logging.ILogger<HybridTenantMiddleware>;

                var mode = DeploymentMode.Online;
                if (modeResolver != null) mode = await modeResolver.GetModeAsync();
                logger?.LogDebug("HybridTenantMiddleware resolving mode={mode}", mode);

                // Platform domain bypass
                var platformDomains = context.RequestServices.GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration)) as Microsoft.Extensions.Configuration.IConfiguration;
                // Prefer X-Forwarded-Host (set by proxy or UI) when present, else use Request.Host
                string? host = null;
                try
                {
                    if (context.Request.Headers.TryGetValue("X-Forwarded-Host", out var xf) && !string.IsNullOrWhiteSpace(xf))
                    {
                        host = xf.ToString().Split(',')[0].Trim();
                    }
                    else if (context.Request.Headers.TryGetValue("Host", out var hhdr) && !string.IsNullOrWhiteSpace(hhdr))
                    {
                        host = hhdr.ToString().Split(',')[0].Trim();
                    }
                    else
                    {
                        host = context.Request.Host.Host;
                    }

                    // Strip port if present (e.g. "example.com:5000" -> "example.com")
                    if (!string.IsNullOrWhiteSpace(host))
                    {
                        var colonIndex = host.IndexOf(':');
                        if (colonIndex > 0) host = host.Substring(0, colonIndex);
                        host = host.Trim();
                    }
                }
                catch { host = context.Request.Host.Host; }
                var platformList = platformDomains?.GetSection("PlatformDomains").Get<string[]>() ?? Array.Empty<string>();
                var isPlatformDomain = Array.Exists(platformList, d => string.Equals(d, host, StringComparison.OrdinalIgnoreCase));

                if (isPlatformDomain)
                {
                    // Platform context - no tenant resolution
                    CurrentTenantAccessor.CurrentTenantId = null;
                    context.Items["TenantId"] = null;
                    logger?.LogDebug("Platform domain matched ({host}), skipping tenant resolution", host);
                }
                else if (mode == DeploymentMode.Online)
                {
                    // In Online mode resolve tenant by host (or X-Debug-Tenant in Development)
                    Guid? tid = null;

                    // Development debug header override
                    var env = context.RequestServices.GetService(typeof(Microsoft.Extensions.Hosting.IHostEnvironment)) as Microsoft.Extensions.Hosting.IHostEnvironment;
                    if (env != null && env.IsDevelopment())
                    {
                        if (context.Request.Headers.TryGetValue("X-Debug-Tenant", out var dbg))
                        {
                            if (Guid.TryParse(dbg, out var dbgGuid)) tid = dbgGuid;
                        }
                    }

                    if (tid == null && tenantResolver != null)
                    {
                        tid = await tenantResolver.ResolveTenantIdFromHostAsync(host);
                    }

                    if (tid.HasValue)
                    {
                        CurrentTenantAccessor.CurrentTenantId = tid.Value;
                        context.Items["TenantId"] = tid.Value;
                        logger?.LogDebug("Online mode: resolved tenant {tid} from host {host}", tid, host);
                    }
                    else
                    {
                        logger?.LogDebug("Online mode: no tenant resolved from host {host}", host);
                    }
                }
                else
                {
                    // OnPrem mode - resolve from AppSettings or config
                    if (tenantResolver != null)
                    {
                        var tid = await tenantResolver.ResolveTenantIdAsync();
                        if (tid.HasValue)
                        {
                            CurrentTenantAccessor.CurrentTenantId = tid.Value;
                            context.Items["TenantId"] = tid.Value;
                            logger?.LogDebug("OnPrem mode: resolved tenant => {tid}", tid);
                        }
                        else
                        {
                            // Fallback: attempt host-based resolution even in OnPrem to support domain-scoped setups
                            try
                            {
                                var hostTid = await tenantResolver.ResolveTenantIdFromHostAsync(host);
                                if (hostTid.HasValue)
                                {
                                    CurrentTenantAccessor.CurrentTenantId = hostTid.Value;
                                    context.Items["TenantId"] = hostTid.Value;
                                    logger?.LogDebug("OnPrem mode fallback: resolved tenant {tid} from host {host}", hostTid, host);
                                }
                                else
                                {
                                    logger?.LogWarning("OnPrem mode: tenant could not be resolved from AppSettings or host");
                                }
                            }
                            catch { logger?.LogWarning("OnPrem mode: host-based fallback failed"); }
                        }
                    }
                }
            }
            catch
            {
                // never break pipeline; leave TenantId absent if resolution fails
            }

            try
            {
                await _next(context);
            }
            finally
            {
                CurrentTenantAccessor.Clear();
            }
        }
    }
}
