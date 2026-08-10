using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HMS.API.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HMS.API.Security;
using HMS.API.Infrastructure.Auth;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notifications;

        public NotificationController(INotificationService notifications)
        {
            _notifications = notifications;
        }

        [HttpGet("recent")]
        [HasPermission("lab.view")]
        public async Task<IActionResult> Recent()
        {
            var items = await _notifications.GetRecentAsync();
            return Ok(items);
        }

        [HttpGet("subscriptions")]
        [Authorize]
        public IActionResult Subscriptions()
        {
            var user = HttpContext.User;
            if (user == null || !user.Identity!.IsAuthenticated) return Unauthorized();

            var channels = new System.Collections.Generic.List<string>();

            var roleNames = user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value);
            if (RoleCatalog.HasAnyRole(roleNames, RoleCatalog.SystemAdministrator, RoleCatalog.HospitalAdministrator, "Admin")) channels.Add("admin");
            if (RoleCatalog.HasAnyRole(roleNames, RoleCatalog.LaboratoryStaff, "LabTech", "Lab")) channels.Add("lab");
            if (RoleCatalog.HasAnyRole(roleNames, RoleCatalog.Pharmacist)) channels.Add("pharmacy");

            // subscribe to patient-specific group if claim present
            var patientClaim = user.FindFirst("patient_id") ?? user.FindFirst(ClaimTypes.NameIdentifier);
            if (patientClaim != null)
            {
                var pid = patientClaim.Value;
                channels.Add($"patient-{pid}");
            }

            return Ok(channels.Distinct());
        }
    }
}
