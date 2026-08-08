using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Pharmacy
{
    public class PurchaseOrderLine : BaseEntity
    {
        public Guid PurchaseOrderId { get; set; }
        public PurchaseOrder PurchaseOrder { get; set; } = null!;

        public Guid ItemId { get; set; }
        public InventoryItem Item { get; set; } = null!;

        public int Quantity { get; set; }

        public Guid? UnitId { get; set; }
        public UnitOfMeasure? Unit { get; set; }

        public decimal UnitPrice { get; set; }
    }
}
