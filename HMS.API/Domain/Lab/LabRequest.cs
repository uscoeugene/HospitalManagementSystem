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

    public class LabRequest : BaseEntity
    {
        public Guid PatientId { get; set; }
        public Guid? VisitId { get; set; }

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
        public string Currency { get; set; } = "USD";

        public Guid? ChargeInvoiceItemId { get; set; } // reference to billing invoice item if created
    }
}