using System.Threading.Tasks;
using System.Collections.Generic;

namespace HMS.API.Application.Billing
{
    public interface IBillingReportService
    {
        Task<BillingSummaryKpiDto> GetSummaryKpiAsync();
        Task<IEnumerable<MonthlyRevenueDto>> GetMonthlyRevenueAsync(int monthsBack);
        Task<IEnumerable<StatusBreakdownDto>> GetInvoiceStatusBreakdownAsync();
        Task<IEnumerable<DailyRevenueDto>> GetDailyRevenueAsync(int daysBack);
        Task<IEnumerable<TopPatientDto>> GetTopPayingPatientsAsync(int top = 10);
        Task<IEnumerable<RefundReportDto>> GetRecentRefundsAsync(int daysBack = 30);
    }
}
