using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using HMS.API.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HMS.API.Application.Common
{
    public class AppSettingsService : IAppSettingsService
    {
        private readonly AuthDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _cfg;
        private readonly MemoryCacheEntryOptions _cacheOpts;

        public AppSettingsService(AuthDbContext db, IMemoryCache cache, IConfiguration cfg)
        {
            _db = db;
            _cache = cache;
            _cfg = cfg;
            var ttl = cfg.GetValue<int?>("TenantCacheTtlSeconds") ?? 300;
            _cacheOpts = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttl) };
        }

        public async Task<string?> GetAsync(string key)
        {
            if (_cache.TryGetValue(key, out string? val)) return val;

            try
            {
                var s = await _db.AppSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Key == key);
                if (s != null)
                {
                    _cache.Set(key, s.Value, _cacheOpts);
                    return s.Value;
                }
            }
            catch
            {
                // swallow DB errors
            }

            // fallback to configuration
            var cfgVal = _cfg[key];
            if (!string.IsNullOrWhiteSpace(cfgVal))
            {
                _cache.Set(key, cfgVal, _cacheOpts);
                return cfgVal;
            }

            return null;
        }

        public async Task SetAsync(string key, string value)
        {
            try
            {
                var e = await _db.AppSettings.SingleOrDefaultAsync(x => x.Key == key);
                if (e == null)
                {
                    e = new HMS.API.Domain.Common.AppSetting { Key = key, Value = value };
                    _db.AppSettings.Add(e);
                }
                else
                {
                    e.Value = value;
                }

                await _db.SaveChangesAsync();
                _cache.Set(key, value, _cacheOpts);
            }
            catch
            {
                // swallow but do not crash
            }
        }

        public Task InvalidateAsync(string key)
        {
            try
            {
                _cache.Remove(key);
            }
            catch { }
            return Task.CompletedTask;
        }
    }
}
