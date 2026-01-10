using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Payments
{
    public class Receipt : BaseEntity
    {
        public Guid PaymentId { get; set; }
        public Payment Payment { get; set; } = null!;

        public string ReceiptNumber { get; set; } = string.Empty;

        public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;

        public string Details { get; set; } = string.Empty;
    }
}