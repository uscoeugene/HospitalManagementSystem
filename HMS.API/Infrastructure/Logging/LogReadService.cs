using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace HMS.API.Infrastructure.Logging
{
    public class LogReadService
    {
        private readonly IConfiguration _cfg;
        private readonly IHostEnvironment _env;
        private readonly string _logDir;

        public LogReadService(IConfiguration cfg, IHostEnvironment env)
        {
            _cfg = cfg;
            _env = env;
            // Use content root path so logs live in the same folder the app writes to (consistent in IIS/console)
            _logDir = Path.Combine(_env.ContentRootPath ?? Directory.GetCurrentDirectory(), "logs");
        }

        public IEnumerable<object> ReadFromFiles(int max = 200)
        {
            if (!Directory.Exists(_logDir)) return Enumerable.Empty<object>();
            var files = Directory.GetFiles(_logDir, "hms-*.log").OrderByDescending(f => f).Take(5);
            var entries = new List<object>();
            foreach (var f in files)
            {
                try
                {
                    var lines = File.ReadAllLines(f).Reverse().Take(max);
                    foreach (var l in lines)
                    {
                        entries.Add(new { source = Path.GetFileName(f), raw = l });
                    }
                }
                catch (Exception ex) { try { System.Diagnostics.Trace.TraceError(ex.ToString()); } catch { } }
            }
            return entries;
        }

        public IEnumerable<object> ReadFromDb(int max = 200)
        {
            var conn = _cfg.GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(conn)) return Enumerable.Empty<object>();

            try
            {
                using var sql = new SqlConnection(conn);
                sql.Open();
                using var cmd = new SqlCommand("SELECT TOP (@top) Id, TimeStamp, [Level], Message, Exception, Properties FROM Logs ORDER BY TimeStamp DESC", sql);
                cmd.Parameters.AddWithValue("@top", max);
                using var rdr = cmd.ExecuteReader();
                var list = new List<object>();
                while (rdr.Read())
                {
                    list.Add(new
                    {
                        id = rdr.IsDBNull(0) ? null : rdr.GetValue(0),
                        timestamp = rdr.IsDBNull(1) ? null : rdr.GetValue(1),
                        level = rdr.IsDBNull(2) ? null : rdr.GetString(2),
                        message = rdr.IsDBNull(3) ? null : rdr.GetString(3),
                        exception = rdr.IsDBNull(4) ? null : rdr.GetString(4),
                        properties = rdr.IsDBNull(5) ? null : rdr.GetString(5)
                    });
                }

                return list;
            }
            catch
            {
                return Enumerable.Empty<object>();
            }
        }
    }
}