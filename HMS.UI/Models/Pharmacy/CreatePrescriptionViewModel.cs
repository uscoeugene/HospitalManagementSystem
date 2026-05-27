using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HMS.UI.Models.Pharmacy
{
    public class CreatePrescriptionItemViewModel
    {
        public Guid InventoryItemId { get; set; }
        public int Quantity { get; set; }
    }

    public class CreatePrescriptionViewModel
    {
        [Required]
        public Guid PatientId { get; set; }
        public Guid? VisitId { get; set; }
        public CreatePrescriptionItemViewModel[] Items { get; set; } = Array.Empty<CreatePrescriptionItemViewModel>();
        public HMS.UI.Models.Pharmacy.InventoryItemViewModel[] AvailableInventoryItems { get; set; } = Array.Empty<HMS.UI.Models.Pharmacy.InventoryItemViewModel>();
    }
}
