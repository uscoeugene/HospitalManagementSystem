using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Payments
{
    public enum PaymentStatus
    {
        PENDING,
        CONFIRMED,
        FAILED
    }

    public class Payment : BaseEntity
    {
        public Guid InvoiceId { get; set; }
        public Guid PatientId { get; set; }

        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";

        public PaymentStatus Status { get; set; } = PaymentStatus.PENDING;

        public string? ExternalReference { get; set; }

        public Guid? ReceiptId { get; set; }
        public Receipt? Receipt { get; set; }

        public Guid CreatedByUserId { get; set; }
    }
}