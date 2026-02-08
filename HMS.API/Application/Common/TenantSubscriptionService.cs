using System;
using System.Text.Json;
using System.Threading.Tasks;
using HMS.API.Domain.Common;
using HMS.API.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;

namespace HMS.API.Application.Common
{
    public class TenantSubscriptionService : ITenantSubscriptionService
    {
        private readonly AuthDbContext _authDb;

        public TenantSubscriptionService(AuthDbContext authDb)
        {
            _authDb = authDb;
        }

        public async Task<TenantSubscription?> GetSubscriptionAsync(Guid tenantId)
        {
            return await _authDb.Set<TenantSubscription>().AsNoTracking().SingleOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted);
        }

        public async Task<bool> IsTenantAllowedAsync(Guid tenantId)
        {
            var sub = await GetSubscriptionAsync(tenantId);
            if (sub == null) return false;
            return sub.IsActive();
        }

        public async Task EnsureTenantActiveOrThrowAsync(Guid tenantId)
        {
            var allowed = await IsTenantAllowedAsync(tenantId);
            if (!allowed) throw new UnauthorizedAccessException("Your subscription is not active");
        }
    }
}
