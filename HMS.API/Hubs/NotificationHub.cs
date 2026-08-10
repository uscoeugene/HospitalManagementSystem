using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using HMS.API.Infrastructure.Auth;

namespace HMS.API.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        public Task Subscribe(string channel)
        {
            return Groups.AddToGroupAsync(Context.ConnectionId, channel);
        }

        public Task Unsubscribe(string channel)
        {
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, channel);
        }

        public override async Task OnConnectedAsync()
        {
            // automatically subscribe to groups based on user roles
            var user = Context.User;
            if (user == null) { await base.OnConnectedAsync(); return; }

            if (RoleCatalog.HasAnyRole(user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value), RoleCatalog.SystemAdministrator, RoleCatalog.HospitalAdministrator, "Admin"))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "admin");
            }

            if (RoleCatalog.HasAnyRole(user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value), RoleCatalog.LaboratoryStaff, "LabTech", "Lab"))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "lab");
            }

            if (RoleCatalog.HasAnyRole(user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value), RoleCatalog.Pharmacist))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "pharmacy");
            }

            // subscribe to patient-specific group if claim present
            var patientClaim = user.FindFirst("patient_id") ?? user.FindFirst(ClaimTypes.NameIdentifier);
            if (patientClaim != null)
            {
                var pid = patientClaim.Value;
                await Groups.AddToGroupAsync(Context.ConnectionId, $"patient-{pid}");
            }

            await base.OnConnectedAsync();
        }
    }
}
