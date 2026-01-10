using System;
using System.Collections.Generic;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Pharmacy
{
    public enum PrescriptionStatus
    {
        ORDERED,
        CHARGED,
        DISPENSED,
        CANCELLED
    }

    public class Prescription : BaseEntity
    {
        public Guid PatientId { get; set; }
        public Guid? VisitId { get; set; }
        public PrescriptionStatus Status { get; set; } = PrescriptionStatus.ORDERED;
        public ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
    }

    public class PrescriptionItem : BaseEntity
    {
        public Guid PrescriptionId { get; set; }
        public Prescription Prescription { get; set; } = null!;
        public Guid DrugId { get; set; }
        public Drug Drug { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = "USD";
        public Guid? ChargeInvoiceItemId { get; set; }
        public int DispensedQuantity { get; set; } = 0;
        public string? Notes { get; set; }
    }
}