using System;
using System.Threading.Tasks;
using HMS.API.Domain.Common;

namespace HMS.API.Application.Common
{
    public interface ITenantSubscriptionService
    {
        Task<TenantSubscription?> GetSubscriptionAsync(Guid tenantId);
        Task<bool> IsTenantAllowedAsync(Guid tenantId);
        Task EnsureTenantActiveOrThrowAsync(Guid tenantId);
    }
}
