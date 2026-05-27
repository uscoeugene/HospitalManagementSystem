using System;

namespace HMS.API.Application.Pharmacy
{
    public class StockShortageDto
    {
        public Guid InventoryItemId { get; set; }
        public string InventoryItemCode { get; set; } = string.Empty;
        public string InventoryItemName { get; set; } = string.Empty;
        public int Stock { get; set; }
        public int Reserved { get; set; }
    }

    public class DailyDispenseDto
    {
        public DateTime Date { get; set; }
        public int DispensedCount { get; set; }
    }

    public class InventoryRevenueDto
    {
        public Guid InventoryItemId { get; set; }
        public string InventoryItemName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
    }
}
