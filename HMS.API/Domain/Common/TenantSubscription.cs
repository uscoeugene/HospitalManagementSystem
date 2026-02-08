using System;

namespace HMS.API.Domain.Common
{
    public enum SubscriptionStatus
    {
        Inactive = 0,
        Trial = 1,
        Active = 2,
        PastDue = 3,
        Suspended = 4,
        Cancelled = 5
    }

    public class TenantSubscription : BaseEntity
    {
        public Guid TenantId { get; set; }

        // e.g. "basic", "pro", "enterprise"
        public string Plan { get; set; } = "basic";

        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Inactive;

        public DateTimeOffset? StartAt { get; set; }
        public DateTimeOffset? EndAt { get; set; }
        public DateTimeOffset? RenewalAt { get; set; }

        // Optional fields for billing provider references
        public string? BillingCustomerId { get; set; }
        public string? BillingSubscriptionId { get; set; }

        // JSON blob for allowed feature flags, quotas, metadata
        public string? MetadataJson { get; set; }

        public bool IsActive()
        {
            if (Status != SubscriptionStatus.Active && Status != SubscriptionStatus.Trial) return false;
            if (EndAt.HasValue && EndAt.Value <= DateTimeOffset.UtcNow) return false;
            return true;
        }
    }
}
