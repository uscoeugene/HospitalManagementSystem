using System.Threading.Tasks;
using HMS.API.Application.Profile;
using HMS.API.Security;
using Microsoft.AspNetCore.Mvc;

namespace HMS.API.Controllers.Reports
{
    [ApiController]
    [Route("reports/[controller]")]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileReportService _reports;

        public ProfileController(IProfileReportService reports)
        {
            _reports = reports;
        }

        [HttpGet("summary")]
        [HasPermission("reports.profiles.view")]
        public async Task<ActionResult> Summary([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var res = await _reports.GetProfileSummaryAsync(page, pageSize);
            return Ok(res);
        }
    }
}
