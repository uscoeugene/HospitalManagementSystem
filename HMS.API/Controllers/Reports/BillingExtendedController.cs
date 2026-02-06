using System.Threading.Tasks;
using HMS.API.Application.Billing;
using HMS.API.Security;
using Microsoft.AspNetCore.Mvc;

namespace HMS.API.Controllers.Reports
{
    [ApiController]
    [Route("api/reports/billing")]
    public class BillingExtendedController : ControllerBase
    {
        private readonly IBillingReportService _reports;

        public BillingExtendedController(IBillingReportService reports)
        {
            _reports = reports;
        }

        [HttpGet("daily")]
        [HasPermission("SERVICE.REPORT.VIEW")]
        public async Task<ActionResult> Daily([FromQuery] int daysBack = 30)
        {
            var res = await _reports.GetDailyRevenueAsync(daysBack);
            return Ok(res);
        }

        [HttpGet("top-patients")]
        [HasPermission("SERVICE.REPORT.VIEW")]
        public async Task<ActionResult> TopPatients([FromQuery] int top = 10)
        {
            var res = await _reports.GetTopPayingPatientsAsync(top);
            return Ok(res);
        }

        [HttpGet("refunds")]
        [HasPermission("SERVICE.REPORT.VIEW")]
        public async Task<ActionResult> Refunds([FromQuery] int daysBack = 30)
        {
            var res = await _reports.GetRecentRefundsAsync(daysBack);
            return Ok(res);
        }
    }
}
