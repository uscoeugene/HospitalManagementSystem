using System;
using System.Collections.Generic;

namespace HMS.API.Application.Pharmacy.DTOs
{
    // DrugDto removed - inventory is managed via InventoryService and InventoryDtos

    public class CreatePrescriptionItem
    {
        public Guid? InventoryItemId { get; set; }
        public string MedicationName { get; set; } = string.Empty;
        public string? Dosage { get; set; }
        public string? Frequency { get; set; }
        public int Quantity { get; set; }
        // When true, this item will be invoiced separately instead of grouped into the prescription invoice
        public bool ChargeSeparately { get; set; } = false;
    }

    public class CreatePrescriptionRequest
    {
        public Guid PatientId { get; set; }
        public Guid? VisitId { get; set; }
        public List<CreatePrescriptionItem> Items { get; set; } = new();
    }

    public class PrescriptionItemDto
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

    public class PrescriptionDto
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public Guid? VisitId { get; set; }
        public string Status { get; set; } = string.Empty;
        public IEnumerable<PrescriptionItemDto> Items { get; set; } = Array.Empty<PrescriptionItemDto>();
    }

    public class DispenseRequest
    {
        public Guid PrescriptionItemId { get; set; }
        public Guid? InventoryItemId { get; set; }
        public string? DispensedMedicationName { get; set; }
        public int Quantity { get; set; }

        // If true, attempt to dispense even if linked invoice is unpaid or partial.
        // Caller must have the 'pharmacy.dispense.credit' permission to use this.
        public bool AllowOnCredit { get; set; } = false;

        // Optional reason or note for allowing credit (e.g., emergency, management order)
        public string? CreditReason { get; set; }
        public string? Note { get; set; }
    }

    public class DispenseDto
    {
        public Guid Id { get; set; }
        public Guid PrescriptionId { get; set; }
        public Guid PrescriptionItemId { get; set; }
        public Guid DispensedBy { get; set; }
        public DateTimeOffset DispensedAt { get; set; }
        public int Quantity { get; set; }
        public bool IsOnCredit { get; set; }
        public string? CreditReason { get; set; }
    }

    public class ReconcilePrescriptionItemRequest
    {
        public Guid? InventoryItemId { get; set; }
        public string? SubstituteMedicationName { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
    }
}
