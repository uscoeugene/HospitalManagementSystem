using System;
using System.Collections.Generic;

namespace HMS.API.Application.Billing.DTOs
{
    public class InvoiceItemDto
    {
        public Guid Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal LineTotal { get; set; }
        public Guid? SourceId { get; set; }
        public string? SourceType { get; set; }
    }

    public class InvoicePaymentDto
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public DateTimeOffset PaidAt { get; set; }
        public string? ExternalReference { get; set; }
        public Guid InvoiceId { get; set; }
    }

    public class InvoiceDto
    {
        public Guid Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public Guid PatientId { get; set; }
        public Guid? VisitId { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public string Currency { get; set; } = "USD";
        public IEnumerable<InvoiceItemDto> Items { get; set; } = Array.Empty<InvoiceItemDto>();
        public IEnumerable<InvoicePaymentDto> Payments { get; set; } = Array.Empty<InvoicePaymentDto>();
    }

    public class ApplyPaymentRequest
    {
        public decimal Amount { get; set; }
        public string? ExternalReference { get; set; }
    }
}