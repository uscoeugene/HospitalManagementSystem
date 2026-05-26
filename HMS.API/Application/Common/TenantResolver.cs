using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using HMS.API.Application.Common;
using HMS.API.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;

namespace HMS.API.Application.Common
{
    public class TenantResolver : ITenantResolver
    {
        private readonly IAppSettingsService _app;
        private readonly IDeploymentModeResolver _mode;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _cfg;
        private readonly AuthDbContext _db;
        private readonly MemoryCacheEntryOptions _opts;
        private readonly ILogger<TenantResolver> _logger;

        public TenantResolver(IAppSettingsService app, IDeploymentModeResolver mode, IMemoryCache cache, IConfiguration cfg, AuthDbContext db, ILogger<TenantResolver> logger)
        {
            _app = app;
            _mode = mode;
            _cache = cache;
            _cfg = cfg;
            _db = db;
            _logger = logger;
            var ttl = cfg.GetValue<int?>("TenantCacheTtlSeconds") ?? 300;
            _opts = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttl) };
        }

        public async Task<Guid?> ResolveTenantIdAsync()
        {
            var mode = await _mode.GetModeAsync();
            if (mode == DeploymentMode.Online)
            {
                // In online mode we don't determine tenant here; request should provide via auth/header.
                return null;
            }

            // OnPrem mode - check cache
            const string keyId = "OnPremise:TenantId";
            if (_cache.TryGetValue(keyId, out Guid tid)) return tid;

            // Try DB-backed AppSettings
            var v = await _app.GetAsync(keyId);
            if (!string.IsNullOrWhiteSpace(v) && Guid.TryParse(v, out var parsed))
            {
                _cache.Set(keyId, parsed, _opts);
                _logger.LogInformation("Resolved OnPremise TenantId from AppSettings DB: {tid}", parsed);
                return parsed;
            }

            // Fallback to configuration
            var cfgVal = _cfg["OnPremise:TenantId"];
            if (!string.IsNullOrWhiteSpace(cfgVal) && Guid.TryParse(cfgVal, out parsed))
            {
                _cache.Set(keyId, parsed, _opts);
                _logger.LogInformation("Resolved OnPremise TenantId from config fallback: {tid}", parsed);
                return parsed;
            }

            return null;
        }

        public async Task<Guid?> ResolveTenantIdFromHostAsync(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return null;
            var key = "domain:" + host.ToLowerInvariant();
            if (_cache.TryGetValue(key, out Guid? cached)) return cached;

            try
            {
                var h = host.ToLowerInvariant();
                var td = await _db.Set<HMS.API.Domain.Common.TenantDomain>().AsNoTracking().SingleOrDefaultAsync(d => d.Domain == h && d.IsActive);
                if (td != null)
                {
                    _cache.Set(key, td.TenantId, _opts);
                    return td.TenantId;
                }

                // fallback: subdomain -> tenant code
                var parts = h.Split('.');
                if (parts.Length > 2)
                {
                    var sub = parts[0];
                    var t = await _db.Tenants.AsNoTracking().SingleOrDefaultAsync(tn => tn.Code.ToLower() == sub);
                    if (t != null)
                    {
                        _cache.Set(key, t.Id, _opts);
                        return t.Id;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve tenant from host {Host}", host);
            }

            _cache.Set(key, null as Guid?, _opts);
            return null;
        }
    }
}
