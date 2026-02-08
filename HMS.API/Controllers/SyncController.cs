using System;
using System.Threading.Tasks;
using HMS.API.Application.Sync;
using Microsoft.AspNetCore.Mvc;
using HMS.API.Security;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SyncController : ControllerBase
    {
        private readonly ISyncManager _manager;

        public SyncController(ISyncManager manager)
        {
            _manager = manager;
        }

        [HttpPost("force")]
        [HasPermission("admin.sync")]
        public async Task<IActionResult> Force()
        {
            await _manager.TriggerAsync();
            return Accepted();
        }

        [HttpGet("status")]
        [HasPermission("admin.sync")]
        public IActionResult Status()
        {
            return Ok(new { running = true, lastRun = System.DateTimeOffset.UtcNow });
        }

        // trigger immediate sync for a tenant from local node to central (pull canonical subscription and other data)
        [HttpPost("tenant/{tenantId}/sync-now")]
        [HasPermission("admin.sync")]
        public async Task<IActionResult> SyncTenantNow(Guid tenantId)
        {
            // trigger tenant-scoped sync
            await _manager.RunOnceAsync(tenantId);
            return Accepted(new { tenantId = tenantId, triggeredAt = DateTimeOffset.UtcNow });
        }
    }
}