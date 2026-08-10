using System;
using System.Threading.Tasks;
using HMS.API.Infrastructure.Auth;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace HMS.API.Application.Common
{
    public class DeploymentModeResolver : IDeploymentModeResolver
    {
        private readonly IAppSettingsService _app;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _cfg;
        private readonly MemoryCacheEntryOptions _opts;
        private readonly ILogger<DeploymentModeResolver> _logger;

        public DeploymentModeResolver(IAppSettingsService app, IMemoryCache cache, IConfiguration cfg)
        {
            _app = app;
            _cache = cache;
            _cfg = cfg;
            var ttl = cfg.GetValue<int?>("TenantCacheTtlSeconds") ?? 300;
            _opts = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = System.TimeSpan.FromSeconds(ttl) };
            _logger = LoggerFactory.Create(b => { }).CreateLogger<DeploymentModeResolver>();
        }

        public async Task<DeploymentMode> GetModeAsync()
        {
            const string key = "System:DeploymentMode";
            if (_cache.TryGetValue(key, out DeploymentMode mode)) return mode;

            var v = await _app.GetAsync(key);
            if (!string.IsNullOrWhiteSpace(v))
            {
                mode = ParseMode(v);
                _cache.Set(key, mode, _opts);
                _logger.LogInformation("DeploymentMode resolved from AppSettings: {mode}", mode);
                return mode;
            }

            var cfg = _cfg.GetValue<string>("Deployment:Mode") ?? "Bootstrap";
            mode = ParseMode(cfg);
            _cache.Set(key, mode, _opts);
            _logger.LogInformation("DeploymentMode resolved from config fallback: {mode}", mode);
            return mode;
        }

        private static DeploymentMode ParseMode(string value)
        {
            if (value.Equals("Online", StringComparison.OrdinalIgnoreCase)) return DeploymentMode.Online;
            if (value.Equals("Bootstrap", StringComparison.OrdinalIgnoreCase)) return DeploymentMode.Bootstrap;
            return DeploymentMode.OnPrem;
        }
    }
}
