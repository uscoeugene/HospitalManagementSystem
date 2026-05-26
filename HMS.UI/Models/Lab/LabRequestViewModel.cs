using System;
using System.Collections.Generic;

namespace HMS.UI.Models.Lab
{
    public class LabRequestItemViewModel
    {
        public Guid Id { get; set; }
        public Guid LabTestId { get; set; }
        public LabTestViewModel? LabTest { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = "USD";
        public Guid? ChargeInvoiceItemId { get; set; }
        public string ResultStatus { get; set; } = "PENDING";
        public string? ResultValue { get; set; }
        public string? ResultUnit { get; set; }
        public string? ReferenceRange { get; set; }
        public string? AbnormalFlag { get; set; }
        public string? ResultNotes { get; set; }
        public string? ResultAttachmentUrl { get; set; }
        public DateTimeOffset? ResultedAt { get; set; }
        public Guid? ResultedByUserId { get; set; }
        public DateTimeOffset? VerifiedAt { get; set; }
        public Guid? VerifiedByUserId { get; set; }
    }

    public class LabRequestViewModel
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public Guid? VisitId { get; set; }
        public string RequestNumber { get; set; } = string.Empty;
        public Guid? InvoiceId { get; set; }
        public string Status { get; set; } = string.Empty;
        public IEnumerable<LabRequestItemViewModel> Items { get; set; } = Array.Empty<LabRequestItemViewModel>();
        public IEnumerable<LabTestViewModel> Tests { get; set; } = Array.Empty<LabTestViewModel>();
        // Optional metadata populated by API or by UI controller for display
        public DateTimeOffset? CreatedAt { get; set; }
        public string? PatientName { get; set; }
        public int ItemsCount { get; set; }
        public string? InvoiceStatus { get; set; }
        public HMS.UI.Models.Billing.InvoiceViewModel? InvoiceSummary { get; set; }
        public string? ResultsStatus { get; set; }
    }
}
