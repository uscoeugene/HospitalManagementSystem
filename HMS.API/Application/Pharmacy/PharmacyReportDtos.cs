using System;

namespace HMS.API.Application.Pharmacy
{
    public class StockShortageDto
    {
        public Guid DrugId { get; set; }
        public string DrugCode { get; set; } = string.Empty;
        public string DrugName { get; set; } = string.Empty;
        public int Stock { get; set; }
        public int Reserved { get; set; }
    }

    public class DailyDispenseDto
    {
        public DateTime Date { get; set; }
        public int DispensedCount { get; set; }
    }

    public class DrugRevenueDto
    {
        public Guid DrugId { get; set; }
        public string DrugName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
    }
}
