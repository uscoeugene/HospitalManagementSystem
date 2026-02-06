using System.Threading.Tasks;
using HMS.API.Application.Lab;
using HMS.API.Security;
using Microsoft.AspNetCore.Mvc;

namespace HMS.API.Controllers.Reports
{
    [ApiController]
    [Route("api/reports/lab")]
    public class LabController : ControllerBase
    {
        private readonly ILabReportService _reports;

        public LabController(ILabReportService reports)
        {
            _reports = reports;
        }

        [HttpGet("status-breakdown")]
        [HasPermission("SERVICE.REPORT.VIEW")]
        public async Task<ActionResult> StatusBreakdown()
        {
            var res = await _reports.GetRequestStatusBreakdownAsync();
            return Ok(res);
        }

        [HttpGet("turnaround")]
        [HasPermission("SERVICE.REPORT.VIEW")]
        public async Task<ActionResult> Turnaround([FromQuery] int recent = 100)
        {
            var res = await _reports.GetTurnaroundTimesAsync(recent);
            return Ok(res);
        }

        [HttpGet("top-tests")]
        [HasPermission("SERVICE.REPORT.VIEW")]
        public async Task<ActionResult> TopTests([FromQuery] int top = 10)
        {
            var res = await _reports.GetTopTestsAsync(top);
            return Ok(res);
        }
    }
}
