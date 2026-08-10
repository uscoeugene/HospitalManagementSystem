using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using HMS.API.Application.Common;
using Microsoft.EntityFrameworkCore;

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
                var appSettings = context.RequestServices.GetService(typeof(IAppSettingsService)) as IAppSettingsService;
                var config = context.RequestServices.GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration)) as Microsoft.Extensions.Configuration.IConfiguration;

                var mode = DeploymentMode.Online;
                if (modeResolver != null) mode = await modeResolver.GetModeAsync();
                logger?.LogDebug("HybridTenantMiddleware resolving mode={mode}", mode);

                string? host = null;
                try
                {
                    string? GetHeaderValue(params string[] names)
                    {
                        foreach (var n in names)
                        {
                            if (context.Request.Headers.TryGetValue(n, out var v) && !string.IsNullOrWhiteSpace(v))
                            {
                                var val = v.ToString().Split(',')[0].Trim();
                                if (!string.IsNullOrWhiteSpace(val)) return val;
                            }
                        }
                        return null;
                    }

                    host = GetHeaderValue("X-Tenant-Host", "X-Original-Host", "X-Forwarded-Host", "X-Host", "X-Forwarded-Server", "Host");
                    if (string.IsNullOrWhiteSpace(host)) host = context.Request.Host.Host;

                    if (!string.IsNullOrWhiteSpace(host))
                    {
                        var colonIndex = host.IndexOf(':');
                        if (colonIndex > 0) host = host.Substring(0, colonIndex);
                        host = host.Trim();
                    }
                    logger?.LogDebug("HybridTenantMiddleware resolved raw host '{HostHeader}' for tenant resolution", host);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to normalize request host for tenant resolution");
                    host = context.Request.Host.Host;
                }

                var platformList = await GetPlatformHostsAsync(appSettings, config);
                var normalizedHost = NormalizeHost(host ?? string.Empty);
                var isPlatformDomain = platformList.Any(d => string.Equals(NormalizeHost(d), normalizedHost, StringComparison.OrdinalIgnoreCase));

                if (isPlatformDomain)
                {
                    CurrentTenantAccessor.CurrentTenantId = null;
                    context.Items["TenantId"] = null;
                    logger?.LogDebug("Platform domain matched ({host}), skipping tenant resolution", host);
                }
                else if (mode == DeploymentMode.Bootstrap)
                {
                    CurrentTenantAccessor.CurrentTenantId = null;
                    context.Items["TenantId"] = null;
                    logger?.LogDebug("Bootstrap mode active; skipping tenant resolution for host {Host}", host);
                }
                else if (mode == DeploymentMode.Online)
                {
                    Guid? tid = null;

                    try
                    {
                        var hdr = context.Request.Headers["X-Tenant-Id"].ToString();
                        if (!string.IsNullOrWhiteSpace(hdr) && Guid.TryParse(hdr, out var parsedHdr))
                        {
                            tid = parsedHdr;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to parse X-Tenant-Id header for tenant resolution");
                    }

                    var env = context.RequestServices.GetService(typeof(Microsoft.Extensions.Hosting.IHostEnvironment)) as Microsoft.Extensions.Hosting.IHostEnvironment;
                    if (env != null && env.IsDevelopment())
                    {
                        if (context.Request.Headers.TryGetValue("X-Debug-Tenant", out var dbg))
                        {
                            if (Guid.TryParse(dbg, out var dbgGuid)) tid = dbgGuid;
                        }
                    }

                    try
                    {
                        var hf = context.Request.Headers;
                        logger?.LogDebug("Tenant resolution headers: Host={HostHeader}, X-Tenant-Host={XTenantHost}, X-Original-Host={XOriginalHost}, X-Forwarded-Host={XForwardedHost}, RemoteIp={RemoteIp}", hf.ContainsKey("Host") ? hf["Host"].ToString() : string.Empty, hf.ContainsKey("X-Tenant-Host") ? hf["X-Tenant-Host"].ToString() : string.Empty, hf.ContainsKey("X-Original-Host") ? hf["X-Original-Host"].ToString() : string.Empty, hf.ContainsKey("X-Forwarded-Host") ? hf["X-Forwarded-Host"].ToString() : string.Empty, context.Connection.RemoteIpAddress?.ToString());
                    }
                    catch (Exception ex) { try { System.Diagnostics.Trace.TraceError(ex.ToString()); } catch { } }

                    if (tid == null && tenantResolver != null)
                    {
                        try
                        {
                            tid = await tenantResolver.ResolveTenantIdFromHostAsync(host ?? string.Empty);
                        }
                        catch (Exception ex)
                        {
                            logger?.LogError(ex, "TenantResolver.ResolveTenantIdFromHostAsync threw for host {Host}", host);
                            throw;
                        }
                    }

                    if (tid.HasValue)
                    {
                        CurrentTenantAccessor.CurrentTenantId = tid.Value;
                        context.Items["TenantId"] = tid.Value;
                        logger?.LogInformation("Online mode: resolved tenant {TenantId} from host {Host}", tid, host);
                        try
                        {
                            var cookieOptions = new Microsoft.AspNetCore.Http.CookieOptions { HttpOnly = false, Secure = context.Request.IsHttps, SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax, Path = "/" };
                            context.Response.Cookies.Append("HmsTenantId", tid.Value.ToString(), cookieOptions);

                            try
                            {
                                var db = context.RequestServices.GetService(typeof(HMS.API.Infrastructure.Auth.AuthDbContext)) as HMS.API.Infrastructure.Auth.AuthDbContext;
                                if (db != null)
                                {
                                    var t = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == tid.Value);
                                    if (t != null && !string.IsNullOrWhiteSpace(t.Name))
                                    {
                                        context.Response.Cookies.Append("HmsTenantName", t.Name, cookieOptions);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                logger?.LogWarning(ex, "Failed to lookup tenant name while setting tenant cookies for {TenantId}", tid.Value);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger?.LogWarning(ex, "Failed to set tenant cookies for {TenantId}", tid.Value);
                        }
                    }
                    else
                    {
                        logger?.LogWarning("Online mode: no tenant resolved from host {Host}. Headers: Host={HostHeader} X-Tenant-Host={XTenantHost} X-Original-Host={XOriginalHost} X-Fwd={XForwardedHost}", host, context.Request.Headers.ContainsKey("Host") ? context.Request.Headers["Host"].ToString() : string.Empty, context.Request.Headers.ContainsKey("X-Tenant-Host") ? context.Request.Headers["X-Tenant-Host"].ToString() : string.Empty, context.Request.Headers.ContainsKey("X-Original-Host") ? context.Request.Headers["X-Original-Host"].ToString() : string.Empty, context.Request.Headers.ContainsKey("X-Forwarded-Host") ? context.Request.Headers["X-Forwarded-Host"].ToString() : string.Empty);
                    }
                }
                else
                {
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
                            try
                            {
                                var hostTid = await tenantResolver.ResolveTenantIdFromHostAsync(host ?? string.Empty);
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
                            catch (Exception ex)
                            {
                                logger?.LogWarning(ex, "OnPrem mode: host-based tenant fallback failed for host {Host}", host);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var logger = context.RequestServices.GetService(typeof(Microsoft.Extensions.Logging.ILogger<HybridTenantMiddleware>)) as Microsoft.Extensions.Logging.ILogger<HybridTenantMiddleware>;
                logger?.LogError(ex, "Hybrid tenant resolution failed; continuing without tenant context");
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

        private static async Task<string[]> GetPlatformHostsAsync(IAppSettingsService? appSettings, Microsoft.Extensions.Configuration.IConfiguration? config)
        {
            if (appSettings != null)
            {
                var dbValue = await appSettings.GetAsync("PlatformContext:Hosts");
                if (!string.IsNullOrWhiteSpace(dbValue)) return SplitHosts(dbValue);

                dbValue = await appSettings.GetAsync("PlatformHosts");
                if (!string.IsNullOrWhiteSpace(dbValue)) return SplitHosts(dbValue);

                dbValue = await appSettings.GetAsync("PlatformDomains");
                if (!string.IsNullOrWhiteSpace(dbValue)) return SplitHosts(dbValue);
            }

            if (config == null) return Array.Empty<string>();

            var preferred = config.GetSection("PlatformContext:Hosts").Get<string[]>();
            if (preferred != null && preferred.Length > 0) return preferred;

            var legacy = config.GetSection("PlatformHosts").Get<string[]>();
            if (legacy != null && legacy.Length > 0) return legacy;

            return config.GetSection("PlatformDomains").Get<string[]>() ?? Array.Empty<string>();
        }

        private static string[] SplitHosts(string value)
        {
            return value
                .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
        }

        private static string NormalizeHost(string host)
        {
            var value = (host ?? string.Empty).Trim().ToLowerInvariant();
            var colonIndex = value.IndexOf(':');
            if (colonIndex > 0)
            {
                value = value.Substring(0, colonIndex);
            }

            return value;
        }
    }
}
