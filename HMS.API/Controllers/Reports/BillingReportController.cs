using System.Threading.Tasks;
using HMS.API.Application.Billing;
using HMS.API.Security;
using Microsoft.AspNetCore.Mvc;

namespace HMS.API.Controllers.Reports
{
    [ApiController]
    [Route("api/reports/[controller]")]
    public class BillingController : ControllerBase
    {
        private readonly IBillingReportService _reports;

        public BillingController(IBillingReportService reports)
        {
            _reports = reports;
        }

        // <summary>
        // Returns high-level billing KPIs: total revenue, invoice counts, paid/unpaid counts and average invoice.
        // Intended for dashboard tiles and quick health checks.
        // </summary>
        // <returns>BillingSummaryKpiDto</returns>
        [HttpGet("kpi")]
        [HasPermission("SERVICE.REPORT.VIEW")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(BillingSummaryKpiDto), 200)]
        public async Task<ActionResult<BillingSummaryKpiDto>> Kpi()
        {
            var res = await _reports.GetSummaryKpiAsync();
            return Ok(res);
        }

        // <summary>
        // Returns monthly revenue series for the last N months.
        // Useful for charts showing revenue trend.
        // </summary>
        // <param name="monthsBack">Number of months to include (default 6)</param>
        [HttpGet("monthly-revenue")]
        [HasPermission("SERVICE.REPORT.VIEW")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(MonthlyRevenueDto[]), 200)]
        public async Task<ActionResult> Monthly([FromQuery] int monthsBack = 6)
        {
            var res = await _reports.GetMonthlyRevenueAsync(monthsBack);
            return Ok(res);
        }

        // <summary>
        // Returns a breakdown of invoices by status (PAID, UNPAID, PARTIAL, etc.).
        // Useful for pie charts / stacked bar visualizations.
        // </summary>
        [HttpGet("status-breakdown")]
        [HasPermission("SERVICE.REPORT.VIEW")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(StatusBreakdownDto[]), 200)]
        public async Task<ActionResult> StatusBreakdown()
        {
            var res = await _reports.GetInvoiceStatusBreakdownAsync();
            return Ok(res);
        }
    }
}
