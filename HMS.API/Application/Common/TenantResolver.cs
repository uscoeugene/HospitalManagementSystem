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

        public TenantResolver(IAppSettingsService app, IDeploymentModeResolver mode, IMemoryCache cache, IConfiguration cfg, AuthDbContext db)
        {
            _app = app;
            _mode = mode;
            _cache = cache;
            _cfg = cfg;
            _db = db;
            var ttl = cfg.GetValue<int?>("TenantCacheTtlSeconds") ?? 300;
            _opts = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttl) };
            _logger = LoggerFactory.Create(b => { }).CreateLogger<TenantResolver>();
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
    }
}
