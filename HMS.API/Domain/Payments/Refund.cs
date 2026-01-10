using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Payments
{
    public class Refund : BaseEntity
    {
        public Guid PaymentId { get; set; }
        public Payment Payment { get; set; } = null!;

        public decimal Amount { get; set; }

        public DateTimeOffset RefundedAt { get; set; } = DateTimeOffset.UtcNow;

        public Guid ProcessedBy { get; set; }

        public string Reason { get; set; } = string.Empty;

        // reversal info
        public bool IsReversed { get; set; } = false;
        public DateTimeOffset? ReversedAt { get; set; }
        public Guid? ReversedBy { get; set; }
        public Guid? ReversalId { get; set; }
    }
}