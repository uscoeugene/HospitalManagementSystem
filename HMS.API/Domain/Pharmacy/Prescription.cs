using System;
using System.Collections.Generic;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Pharmacy
{
    public enum PrescriptionStatus
    {
        ORDERED,
        IN_PHARMACY,
        DISPENSED,
        CANCELLED
    }

    public enum PrescriptionItemStatus
    {
        PENDING,
        READY,
        OUT_OF_STOCK,
        ORDER_STOCK,
        UNAVAILABLE,
        PARTIALLY_DISPENSED,
        DISPENSED,
        SUBSTITUTED
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
        public Guid? InventoryItemId { get; set; }
        public InventoryItem? InventoryItem { get; set; }
        public string MedicationName { get; set; } = string.Empty;
        public string? Dosage { get; set; }
        public string? Frequency { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = "NGN";
        public Guid? ChargeInvoiceItemId { get; set; }
        // If true, this item should be invoiced separately (per-item invoice) instead of being grouped
        // into a single prescription invoice. Default: false (grouped invoicing).
        public bool ChargeSeparately { get; set; } = false;
        public int DispensedQuantity { get; set; } = 0;
        public string? Notes { get; set; }
        public PrescriptionItemStatus FulfillmentStatus { get; set; } = PrescriptionItemStatus.PENDING;
        public string? ShortageReason { get; set; }
        public bool IsSubstituted { get; set; }
        public string? SubstituteMedicationName { get; set; }
    }
}
