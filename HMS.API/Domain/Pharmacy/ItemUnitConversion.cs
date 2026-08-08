using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Pharmacy
{
    public class ItemUnitConversion : BaseEntity
    {
        public Guid ItemId { get; set; }
        public InventoryItem Item { get; set; } = null!;

        public Guid UnitId { get; set; }
        public UnitOfMeasure Unit { get; set; } = null!;

        // how many base units this unit represents
        public int BaseUnitQty { get; set; }
    }
}
