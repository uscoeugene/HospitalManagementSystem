using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Pharmacy
{
    public enum StockTransactionType
    {
        PURCHASE,
        SALE,
        DISPENSE,
        TRANSFER_IN,
        TRANSFER_OUT,
        RETURN,
        ADJUSTMENT,
        DAMAGE,
        EXPIRY,
        CONSUMPTION
    }

    public class StockTransaction : BaseEntity
    {
        public Guid ItemId { get; set; }
        public InventoryItem Item { get; set; } = null!;

        public Guid? BatchId { get; set; }
        public InventoryBatch? Batch { get; set; }

        public Guid StoreId { get; set; }
        public Store Store { get; set; } = null!;

        public StockTransactionType TransactionType { get; set; }
        public DateTimeOffset Date { get; set; } = DateTimeOffset.UtcNow;

        // positive for additions, negative for removals
        public int Quantity { get; set; }

        public decimal UnitCost { get; set; }

        public string? ReferenceType { get; set; }
        public Guid? ReferenceId { get; set; }

        public Guid CreatedBy { get; set; }
    }
}
