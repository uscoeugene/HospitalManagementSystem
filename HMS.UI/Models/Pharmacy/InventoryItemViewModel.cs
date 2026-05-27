using System;

namespace HMS.UI.Models.Pharmacy
{
    public class InventoryItemViewModel
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal UnitPrice { get; set; }
        public string Currency { get; set; } = "USD";
        public int Stock { get; set; }
        public int ReservedStock { get; set; }
        public string? Category { get; set; }
        public string? Unit { get; set; }
    }
}
