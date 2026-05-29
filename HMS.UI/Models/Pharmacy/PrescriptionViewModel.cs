using System;
using System.Collections.Generic;

namespace HMS.UI.Models.Pharmacy
{
    public class PrescriptionItemViewModel
    {
        public Guid Id { get; set; }
        public Guid? InventoryItemId { get; set; }
        public string MedicationName { get; set; } = string.Empty;
        public string? InventoryItemName { get; set; }
        public string? Dosage { get; set; }
        public string? Frequency { get; set; }
        public int Quantity { get; set; }
        public int DispensedQuantity { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = "NGN";
        public string? Notes { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ShortageReason { get; set; }
        public bool IsSubstituted { get; set; }
        public string? SubstituteMedicationName { get; set; }
        public int? AvailableStock { get; set; }
    }

    public class PrescriptionViewModel
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public Guid? VisitId { get; set; }
        // UI friendly fields
        public string? PatientDisplay { get; set; }
        public string? VisitDisplay { get; set; }
        public string Status { get; set; } = string.Empty;
        public PrescriptionItemViewModel[] Items { get; set; } = Array.Empty<PrescriptionItemViewModel>();
    }
}
