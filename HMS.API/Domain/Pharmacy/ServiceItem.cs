using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Pharmacy
{
    public class ServiceItem : BaseEntity
    {
        public Guid ServiceId { get; set; }
        public Service Service { get; set; } = null!;

        public Guid ItemId { get; set; }
        public InventoryItem Item { get; set; } = null!;

        public int Quantity { get; set; }

        public Guid? UnitId { get; set; }
        public UnitOfMeasure? Unit { get; set; }
    }
}
