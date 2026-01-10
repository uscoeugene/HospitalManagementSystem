using System;

namespace HMS.API.Application.Payments.DTOs
{
    public class CreatePaymentRequest
    {
        public Guid InvoiceId { get; set; }
        public Guid PatientId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string? ExternalReference { get; set; }
    }

    public class PaymentDto
    {
        public Guid Id { get; set; }
        public Guid InvoiceId { get; set; }
        public Guid PatientId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string Status { get; set; } = string.Empty;
        public string? ExternalReference { get; set; }
    }

    public class ReceiptDto
    {
        public Guid Id { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public Guid PaymentId { get; set; }
        public DateTimeOffset IssuedAt { get; set; }
        public string Details { get; set; } = string.Empty;
    }

    public class RefundRequest
    {
        public decimal Amount { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class RefundDto
    {
        public Guid Id { get; set; }
        public Guid PaymentId { get; set; }
        public decimal Amount { get; set; }
        public DateTimeOffset RefundedAt { get; set; }
        public Guid ProcessedBy { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class RefundReversalRequest
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class RefundReversalDto
    {
        public Guid Id { get; set; }
        public Guid RefundId { get; set; }
        public Guid ProcessedBy { get; set; }
        public DateTimeOffset ProcessedAt { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}