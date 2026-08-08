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
        public string Currency { get; set; } = "NGN";
        public int Stock { get; set; }
        public int ReservedStock { get; set; }

        // dynamic category reference
        public Guid? CategoryId { get; set; }
        public InventoryCategory? Category { get; set; }

        // backward-compatible free-text unit (kept for UI compatibility)
        public string? Unit { get; set; } // e.g., box, piece, vial

        // New structured inventory model fields
        // Reference to canonical base unit for the item (optional)
        public Guid? BaseUnitId { get; set; }
        public UnitOfMeasure? BaseUnit { get; set; }

        // Tracking flags
        public bool IsBatchTracked { get; set; } = false;
        public bool IsExpiryTracked { get; set; } = false;
        public bool IsActive { get; set; } = true;
    }
}