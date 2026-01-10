using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Payments
{
    public class RefundReversal : BaseEntity
    {
        public Guid RefundId { get; set; }
        public Refund Refund { get; set; } = null!;

        public Guid ProcessedBy { get; set; }
        public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
        public string Reason { get; set; } = string.Empty;
    }
}