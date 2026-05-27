using System;

namespace HMS.UI.Models.Pharmacy
{
    public class InventoryRevenueViewModel
    {
        public Guid InventoryItemId { get; set; }
        public string InventoryItemName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
    }
}
