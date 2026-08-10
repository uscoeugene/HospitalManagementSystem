using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
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
            if (mode != DeploymentMode.OnPrem)
            {
                return null;
            }

            const string keyId = "OnPremise:TenantId";
            if (_cache.TryGetValue(keyId, out Guid tid)) return tid;

            var v = await _app.GetAsync(keyId);
            if (!string.IsNullOrWhiteSpace(v) && Guid.TryParse(v, out var parsed))
            {
                _cache.Set(keyId, parsed, _opts);
                _logger.LogInformation("Resolved OnPremise TenantId from AppSettings DB: {tid}", parsed);
                return parsed;
            }

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
            var normalizedHost = NormalizeHost(host);
            var key = "domain:" + normalizedHost;
            if (_cache.TryGetValue(key, out Guid? cached)) return cached;

            try
            {
                if (await IsPlatformHostAsync(normalizedHost))
                {
                    _cache.Set(key, null as Guid?, _opts);
                    return null;
                }

                var td = await _db.Set<HMS.API.Domain.Common.TenantDomain>().AsNoTracking().SingleOrDefaultAsync(d => d.Domain == normalizedHost && d.IsActive);
                if (td != null)
                {
                    _cache.Set(key, td.TenantId, _opts);
                    return td.TenantId;
                }

                var parts = normalizedHost.Split('.');
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

        private async Task<bool> IsPlatformHostAsync(string host)
        {
            var configured = await GetPlatformHostsAsync();
            return configured.Any(x => string.Equals(NormalizeHost(x), host, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<string[]> GetPlatformHostsAsync()
        {
            var dbValue = await _app.GetAsync("PlatformContext:Hosts");
            if (!string.IsNullOrWhiteSpace(dbValue)) return SplitHosts(dbValue);

            dbValue = await _app.GetAsync("PlatformHosts");
            if (!string.IsNullOrWhiteSpace(dbValue)) return SplitHosts(dbValue);

            dbValue = await _app.GetAsync("PlatformDomains");
            if (!string.IsNullOrWhiteSpace(dbValue)) return SplitHosts(dbValue);

            var preferred = _cfg.GetSection("PlatformContext:Hosts").Get<string[]>();
            if (preferred != null && preferred.Length > 0) return preferred;

            var legacy = _cfg.GetSection("PlatformHosts").Get<string[]>();
            if (legacy != null && legacy.Length > 0) return legacy;

            return _cfg.GetSection("PlatformDomains").Get<string[]>() ?? Array.Empty<string>();
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
            var value = host.Trim().ToLowerInvariant();
            var colonIndex = value.IndexOf(':');
            if (colonIndex > 0)
            {
                value = value.Substring(0, colonIndex);
            }

            return value;
        }
    }
}
