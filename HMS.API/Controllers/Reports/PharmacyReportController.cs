using System.Threading.Tasks;
using HMS.API.Application.Pharmacy;
using HMS.API.Security;
using Microsoft.AspNetCore.Mvc;

namespace HMS.API.Controllers.Reports
{
    [ApiController]
    [Route("api/reports/pharmacy")]
    public class PharmacyController : ControllerBase
    {
        private readonly IPharmacyReportService _reports;

        public PharmacyController(IPharmacyReportService reports)
        {
            _reports = reports;
        }

        [HttpGet("shortages")]
        [HasPermission("SERVICE.REPORT.VIEW")]
        public async Task<ActionResult> Shortages([FromQuery] int threshold = 5)
        {
            var res = await _reports.GetStockShortagesAsync(threshold);
            return Ok(res);
        }

        [HttpGet("daily-dispenses")]
        [HasPermission("SERVICE.REPORT.VIEW")]
        public async Task<ActionResult> DailyDispenses([FromQuery] int daysBack = 30)
        {
            var res = await _reports.GetDailyDispensesAsync(daysBack);
            return Ok(res);
        }

        [HttpGet("revenue-per-drug")]
        [HasPermission("SERVICE.REPORT.VIEW")]
        public async Task<ActionResult> RevenuePerDrug([FromQuery] int monthsBack = 6)
        {
            var res = await _reports.GetRevenuePerDrugAsync(monthsBack);
            return Ok(res);
        }
    }
}
