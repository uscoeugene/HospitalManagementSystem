using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Billing
{
    public class DebtorEntry : BaseEntity
    {
        public Guid InvoiceId { get; set; }
        // legacy: prescription item id removed in favor of SourceItemId/SourceType
        public Guid? SourceItemId { get; set; }
        public string? SourceType { get; set; }

        public decimal AmountOwed { get; set; }
        public string? Reason { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Resolution fields
        public bool IsResolved { get; set; } = false;
        public DateTimeOffset? ResolvedAt { get; set; }
        public Guid? ResolvedBy { get; set; }

        // Notification tracking
        public bool NotificationSent { get; set; } = false;
        public DateTimeOffset? NotificationSentAt { get; set; }
        public int NotificationAttempts { get; set; } = 0;
    }
}
