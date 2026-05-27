using System;

namespace HMS.UI.Models.Pharmacy
{
    public class PharmacyReportsViewModel
    {
        public StockShortageViewModel[] Shortages { get; set; } = Array.Empty<StockShortageViewModel>();
        public DailyDispenseViewModel[] Daily { get; set; } = Array.Empty<DailyDispenseViewModel>();
        public InventoryRevenueViewModel[] Revenue { get; set; } = Array.Empty<InventoryRevenueViewModel>();
    }
}
