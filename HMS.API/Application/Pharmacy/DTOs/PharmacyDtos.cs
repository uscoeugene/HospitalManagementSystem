using System;
using System.Collections.Generic;

namespace HMS.API.Application.Pharmacy.DTOs
{
    // DrugDto removed - inventory is managed via InventoryService and InventoryDtos

    public class CreatePrescriptionItem
    {
        public Guid InventoryItemId { get; set; }
        public int Quantity { get; set; }
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
        public Guid InventoryItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int DispensedQuantity { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = "USD";
        public string? Notes { get; set; }
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
        public int Quantity { get; set; }

        // If true, attempt to dispense even if linked invoice is unpaid or partial.
        // Caller must have the 'pharmacy.dispense.credit' permission to use this.
        public bool AllowOnCredit { get; set; } = false;

        // Optional reason or note for allowing credit (e.g., emergency, management order)
        public string? CreditReason { get; set; }
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
}