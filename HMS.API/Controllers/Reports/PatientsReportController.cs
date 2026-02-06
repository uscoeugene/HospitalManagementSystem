using System.Threading.Tasks;
using HMS.API.Application.Patient;
using HMS.API.Security;
using Microsoft.AspNetCore.Mvc;

namespace HMS.API.Controllers.Reports
{
    [ApiController]
    [Route("reports/[controller]")]
    public class PatientsController : ControllerBase
    {
        private readonly IPatientReportService _reports;

        public PatientsController(IPatientReportService reports)
        {
            _reports = reports;
        }

        [HttpGet("summary")]
        [HasPermission("reports.patients.view")]
        public async Task<ActionResult> Summary([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var res = await _reports.GetPatientSummaryAsync(page, pageSize);
            return Ok(res);
        }
    }
}
