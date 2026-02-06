using System.Collections.Generic;
using System.Threading.Tasks;

namespace HMS.API.Application.Pharmacy
{
    public interface IPharmacyReportService
    {
        Task<IEnumerable<StockShortageDto>> GetStockShortagesAsync(int threshold = 5);
        Task<IEnumerable<DailyDispenseDto>> GetDailyDispensesAsync(int daysBack = 30);
        Task<IEnumerable<DrugRevenueDto>> GetRevenuePerDrugAsync(int monthsBack = 6);
    }
}
