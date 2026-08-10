using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using HMS.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("logs")]
    public class LogsController : ControllerBase
    {
        private readonly IConfiguration _cfg;
        private readonly IHostEnvironment _env;
        private readonly HmsDbContext _db;

        public LogsController(IConfiguration cfg, IHostEnvironment env, HmsDbContext db)
        {
            _cfg = cfg;
            _env = env;
            _db = db;
        }

        // GET /logs?source=db|file&max=200&level=Error&from=2026-06-01&to=2026-06-06&page=1&pageSize=50
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string source = "db", [FromQuery] int max = 200,
            [FromQuery] string? level = null, [FromQuery] DateTimeOffset? from = null, [FromQuery] DateTimeOffset? to = null,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            if (string.Equals(source, "db", StringComparison.OrdinalIgnoreCase))
            {
                // Use EF to query Logs table with optional filters and paging
                try
                {
                    var q = _db.Logs.AsNoTracking().AsQueryable();
                    if (!string.IsNullOrWhiteSpace(level)) q = q.Where(l => l.Level != null && l.Level == level);
                    if (from.HasValue) q = q.Where(l => l.TimeStamp >= from.Value);
                    if (to.HasValue) q = q.Where(l => l.TimeStamp <= to.Value);

                    var total = await q.CountAsync();

                    // normalize paging
                    page = Math.Max(1, page);
                    pageSize = Math.Clamp(pageSize, 1, 1000);

                    var items = await q.OrderByDescending(l => l.TimeStamp)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(l => new { id = l.Id, timestamp = l.TimeStamp, level = l.Level, message = l.Message, exception = l.Exception, properties = l.Properties })
                        .ToListAsync();

                    return Ok(new { items, total, page, pageSize });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { error = "DB query failed", detail = ex.Message });
                }
            }

            // file fallback
            try
            {
                var logDir = Path.Combine(_env.ContentRootPath, "logs");
                if (!Directory.Exists(logDir)) return Ok(Array.Empty<object>());
                var files = Directory.GetFiles(logDir, "hms-*.log").OrderByDescending(f => f).Take(5);
                var entries = new List<object>();
                foreach (var f in files)
                {
                    try
                    {
                        var lines = System.IO.File.ReadAllLines(f).Reverse().Take(max);
                        foreach (var l in lines)
                        {
                            entries.Add(new { source = Path.GetFileName(f), raw = l });
                        }
                    }
                    catch (Exception ex) { try { System.Diagnostics.Trace.TraceError(ex.ToString()); } catch { } }
                }

                return Ok(entries);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}