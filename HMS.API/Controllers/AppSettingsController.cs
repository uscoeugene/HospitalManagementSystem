using System.Threading.Tasks;
using HMS.API.Infrastructure.Auth;
using HMS.API.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HMS.API.Security;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AppSettingsController : ControllerBase
    {
        private readonly AuthDbContext _db;

        public AppSettingsController(AuthDbContext db)
        {
            _db = db;
        }

        [HttpGet("{key}")]
        public async Task<ActionResult<string>> Get(string key)
        {
            var s = await _db.AppSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Key == key);
            if (s == null) return NotFound();
            return Ok(new { key = s.Key, value = s.Value });
        }

        [HttpGet]
        public async Task<ActionResult> List()
        {
            var items = await _db.AppSettings.AsNoTracking().OrderBy(a => a.Key).ToListAsync();
            return Ok(items.Select(i => new { key = i.Key, value = i.Value }));
        }

        [HttpPost("upsert")]
        [HasPermission("users.manage")]
        public async Task<IActionResult> Upsert([FromBody] AppSettingDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Key)) return BadRequest(new { error = "Key required" });

            var e = await _db.AppSettings.SingleOrDefaultAsync(x => x.Key == dto.Key);
            if (e == null)
            {
                e = new AppSetting { Key = dto.Key, Value = dto.Value ?? string.Empty };
                _db.AppSettings.Add(e);
            }
            else
            {
                e.Value = dto.Value ?? string.Empty;
            }

            await _db.SaveChangesAsync();
            // invalidate cache entry via AppSettingsService if available
            try
            {
                var svc = HttpContext.RequestServices.GetService(typeof(HMS.API.Application.Common.IAppSettingsService)) as HMS.API.Application.Common.IAppSettingsService;
                if (svc != null) await svc.InvalidateAsync(dto.Key);
            }
            catch { }
            return Ok(new { key = e.Key, value = e.Value });
        }
    }

    public class AppSettingDto { public string Key { get; set; } = string.Empty; public string? Value { get; set; } }
}
