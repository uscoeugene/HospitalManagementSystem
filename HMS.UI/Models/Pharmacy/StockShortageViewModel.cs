using System;

namespace HMS.UI.Models.Pharmacy
{
    public class StockShortageViewModel
    {
        public Guid InventoryItemId { get; set; }
        public string InventoryItemCode { get; set; } = string.Empty;
        public string InventoryItemName { get; set; } = string.Empty;
        public int Stock { get; set; }
        public int Reserved { get; set; }
    }
}
