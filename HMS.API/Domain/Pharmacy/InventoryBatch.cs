using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Pharmacy
{
    public class InventoryBatch : BaseEntity
    {
        public Guid ItemId { get; set; }
        public InventoryItem Item { get; set; } = null!;

        public Guid StoreId { get; set; }
        public Store Store { get; set; } = null!;

        public string BatchNumber { get; set; } = string.Empty;
        public DateOnly? ExpiryDate { get; set; }
        public DateOnly? ManufactureDate { get; set; }

        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }

        public int ReceivedQty { get; set; }
        public int AvailableQty { get; set; }
    }
}
