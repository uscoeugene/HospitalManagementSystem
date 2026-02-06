using System.Threading.Tasks;
using HMS.API.Application.Billing;
using HMS.API.Application.Patient;
using HMS.API.Application.Profile;
using HMS.API.Security;
using Microsoft.AspNetCore.Mvc;

namespace HMS.API.Controllers.Reports
{
    [ApiController]
    [Route("api/reports/admin")]
    public class DashboardController : ControllerBase
    {
        private readonly IBillingReportService _billing;
        private readonly IPatientReportService _patients;
        private readonly IProfileReportService _profiles;

        public DashboardController(IBillingReportService billing, IPatientReportService patients, IProfileReportService profiles)
        {
            _billing = billing;
            _patients = patients;
            _profiles = profiles;
        }

        [HttpGet("dashboard")]
        [HasPermission("ADMIN.DASHBOARD.VIEW")]
        public async Task<ActionResult> Get()
        {
            // Compose small set of KPIs from services without direct DB coupling
            var billingKpi = await _billing.GetSummaryKpiAsync();
            var patientSummary = await _patients.GetPatientSummaryAsync(1, 5);
            var profileSummary = await _profiles.GetProfileSummaryAsync(1, 5);

            var res = new
            {
                Billing = billingKpi,
                RecentPatients = patientSummary,
                RecentProfiles = profileSummary
            };

            return Ok(res);
        }
    }
}
