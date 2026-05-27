using System;
using System.Collections.Generic;

namespace HMS.API.Application.Lab.DTOs
{
    public class LabTestDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Currency { get; set; } = "NGN";
    }

    public class CreateLabRequestItem
    {
        public Guid LabTestId { get; set; }
    }

    public class CreateLabRequest
    {
        public Guid PatientId { get; set; }
        public Guid? VisitId { get; set; }
        public List<CreateLabRequestItem> Items { get; set; } = new();

        // Allow creating lab requests and charging items on credit (create debts) when true.
        public bool AllowOnCredit { get; set; } = false;
        public string? CreditReason { get; set; }
    }

    public class LabRequestItemDto
    {
        public Guid Id { get; set; }
        public Guid LabTestId { get; set; }
        public LabTestDto LabTest { get; set; } = new();
        public decimal Price { get; set; }
        public string Currency { get; set; } = "NGN";
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

    public class UpdateLabResultRequest
    {
        public string? ResultValue { get; set; }
        public string? ResultUnit { get; set; }
        public string? ReferenceRange { get; set; }
        public string? AbnormalFlag { get; set; }
        public string? ResultNotes { get; set; }
        public bool Verify { get; set; }
    }

    public class LabRequestDto
    {
        public Guid Id { get; set; }
        public string RequestNumber { get; set; } = string.Empty;
        public Guid PatientId { get; set; }
        public Guid? VisitId { get; set; }
        public Guid? InvoiceId { get; set; }
        public string Status { get; set; } = string.Empty;
        public IEnumerable<LabRequestItemDto> Items { get; set; } = Array.Empty<LabRequestItemDto>();
        public IEnumerable<LabTestDto> Tests { get; set; } = Array.Empty<LabTestDto>();
        // Additional metadata for UI convenience
        public DateTimeOffset? CreatedAt { get; set; }
        public string? PatientName { get; set; }
        public InvoiceSummaryDto? InvoiceSummary { get; set; }
        public int ItemsCount { get; set; }
        public string? ResultsStatus { get; set; }
    }

    public class InvoiceSummaryDto
    {
        public Guid Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public string Currency { get; set; } = "NGN";
    }
}
