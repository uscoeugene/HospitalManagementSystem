using System;

namespace HMS.UI.Models.Pharmacy
{
    public class EditPrescriptionViewModel
    {
        public PrescriptionViewModel Prescription { get; set; } = new PrescriptionViewModel();
        public InventoryItemViewModel[] AvailableInventoryItems { get; set; } = Array.Empty<InventoryItemViewModel>();
    }
}
