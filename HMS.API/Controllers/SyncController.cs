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
    }
}