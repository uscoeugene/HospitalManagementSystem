using System;
using System.Collections.Generic;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Lab
{
    public enum LabRequestStatus
    {
        ORDERED,
        CHARGED,
        PROCESSING,
        COMPLETED,
        CANCELLED
    }

    public enum LabResultStatus
    {
        PENDING,
        RESULTED,
        VERIFIED,
        AMENDED
    }

    public class LabRequest : BaseEntity
    {
        public Guid PatientId { get; set; }
        public Guid? VisitId { get; set; }
        // Human-readable request/code (e.g. LR-20240501-xxx)
        public string RequestNumber { get; set; } = string.Empty;

        // Linked invoice created when charging the lab request (optional)
        public Guid? InvoiceId { get; set; }

        public LabRequestStatus Status { get; set; } = LabRequestStatus.ORDERED;

        public ICollection<LabRequestItem> Items { get; set; } = new List<LabRequestItem>();
    }

    public class LabRequestItem : BaseEntity
    {
        public Guid LabRequestId { get; set; }
        public LabRequest LabRequest { get; set; } = null!;

        public Guid LabTestId { get; set; }
        public LabTest LabTest { get; set; } = null!;

        public decimal Price { get; set; }
        public string Currency { get; set; } = "NGN";

        public Guid? ChargeInvoiceItemId { get; set; } // reference to billing invoice item if created

        public LabResultStatus ResultStatus { get; set; } = LabResultStatus.PENDING;
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
}
