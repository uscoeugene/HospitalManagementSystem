using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Pharmacy
{
    public class DispenseTransaction : BaseEntity
    {
        public Guid PrescriptionItemId { get; set; }
        public Guid? BatchId { get; set; }
        public InventoryBatch? Batch { get; set; }

        public int Quantity { get; set; }

        public Guid DispensedBy { get; set; }
        public DateTimeOffset DispensedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
