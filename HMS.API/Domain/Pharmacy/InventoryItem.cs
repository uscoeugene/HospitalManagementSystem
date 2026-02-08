using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Pharmacy
{
    public class InventoryItem : BaseEntity
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal UnitPrice { get; set; }
        public string Currency { get; set; } = "USD";
        public int Stock { get; set; }
        public int ReservedStock { get; set; }

        // dynamic category reference
        public Guid? CategoryId { get; set; }
        public InventoryCategory? Category { get; set; }

        public string? Unit { get; set; } // e.g., box, piece, vial
    }
}