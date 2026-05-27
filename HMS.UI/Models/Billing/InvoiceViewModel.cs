using System;
using System.Collections.Generic;

namespace HMS.UI.Models.Billing
{
    public class InvoiceItemViewModel
    {
        public Guid Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal LineTotal { get; set; }
        public Guid? SourceId { get; set; }
        public string? SourceType { get; set; }
    }

    public class InvoicePaymentViewModel
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public DateTimeOffset PaidAt { get; set; }
        public string? ExternalReference { get; set; }
        public Guid InvoiceId { get; set; }
    }

    public class InvoiceViewModel
    {
        public Guid Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public Guid PatientId { get; set; }
        public Guid? VisitId { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public string Currency { get; set; } = "USD";
        // UI-friendly fields
        public string? PatientName { get; set; }
        public DateTimeOffset? VisitAt { get; set; }
        public string? VisitType { get; set; }
        public string? Source { get; set; }
        public decimal Balance { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public string? UpdatedByName { get; set; }
        public InvoiceItemViewModel[] Items { get; set; } = Array.Empty<InvoiceItemViewModel>();
        public InvoicePaymentViewModel[] Payments { get; set; } = Array.Empty<InvoicePaymentViewModel>();
    }
}
