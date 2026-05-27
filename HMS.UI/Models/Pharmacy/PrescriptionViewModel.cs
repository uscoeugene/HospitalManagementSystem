using System;
using System.Collections.Generic;

namespace HMS.UI.Models.Pharmacy
{
    public class PrescriptionItemViewModel
    {
        public Guid Id { get; set; }
        public Guid InventoryItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int DispensedQuantity { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = "USD";
        public string? Notes { get; set; }
    }

    public class PrescriptionViewModel
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public Guid? VisitId { get; set; }
        public string Status { get; set; } = string.Empty;
        public PrescriptionItemViewModel[] Items { get; set; } = Array.Empty<PrescriptionItemViewModel>();
    }
}
