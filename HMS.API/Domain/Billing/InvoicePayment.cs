using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Billing
{
    public class InvoicePayment : BaseEntity
    {
        public Guid InvoiceId { get; set; }
        public Invoice Invoice { get; set; } = null!;

        public decimal Amount { get; set; }

        public DateTimeOffset PaidAt { get; set; } = DateTimeOffset.UtcNow;

        public string? ExternalReference { get; set; } // payment gateway / receipt reference
    }
}