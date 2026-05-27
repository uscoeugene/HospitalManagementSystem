using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace HMS.UI.Models.Pharmacy
{
    public class PrescriptionPatientOptionViewModel
    {
        public Guid Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    public class PrescriptionVisitOptionViewModel
    {
        public Guid Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    public class CreatePrescriptionItemViewModel
    {
        public Guid? InventoryItemId { get; set; }

        [Display(Name = "Medication name")]
        public string MedicationName { get; set; } = string.Empty;

        public string? Dosage { get; set; }
        public string? Frequency { get; set; }
        public int Quantity { get; set; }
    }

    public class CreatePrescriptionViewModel
    {
        [Required]
        public Guid PatientId { get; set; }

        public Guid? VisitId { get; set; }

        public CreatePrescriptionItemViewModel[] Items { get; set; } = Array.Empty<CreatePrescriptionItemViewModel>();

        public InventoryItemViewModel[] AvailableInventoryItems { get; set; } = Array.Empty<InventoryItemViewModel>();
        public PrescriptionPatientOptionViewModel[] AvailablePatients { get; set; } = Array.Empty<PrescriptionPatientOptionViewModel>();
        public PrescriptionVisitOptionViewModel[] AvailableVisits { get; set; } = Array.Empty<PrescriptionVisitOptionViewModel>();
        public bool LockPatientSelection { get; set; }
        public bool LockVisitSelection { get; set; }
        public string? ReturnUrl { get; set; }

        public CreatePrescriptionItemViewModel[] GetNormalizedItems(int minimumCount = 1)
        {
            var rows = (Items ?? Array.Empty<CreatePrescriptionItemViewModel>()).ToList();
            if (rows.Count == 0)
            {
                rows.Add(new CreatePrescriptionItemViewModel { Quantity = 1 });
            }

            while (rows.Count < minimumCount)
            {
                rows.Add(new CreatePrescriptionItemViewModel { Quantity = 1 });
            }

            return rows.ToArray();
        }
    }
}
