using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Pharmacy
{
    public class DispenseLog : BaseEntity
    {
        public Guid PrescriptionId { get; set; }
        public Guid PrescriptionItemId { get; set; }
        public Guid? InventoryItemId { get; set; }
        public string MedicationName { get; set; } = string.Empty;
        public Guid DispensedBy { get; set; }
        public DateTimeOffset DispensedAt { get; set; } = DateTimeOffset.UtcNow;
        public int Quantity { get; set; }
        public string? Notes { get; set; }
    }
}
