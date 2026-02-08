using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Pharmacy
{
    public class InventoryAudit : BaseEntity
    {
        public Guid InventoryItemId { get; set; }
        public InventoryItem InventoryItem { get; set; } = null!;

        public string ChangeType { get; set; } = string.Empty; // Created, Updated, StockAdjusted, Deleted
        public string? Details { get; set; }
        public Guid PerformedBy { get; set; }
        public DateTimeOffset PerformedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
