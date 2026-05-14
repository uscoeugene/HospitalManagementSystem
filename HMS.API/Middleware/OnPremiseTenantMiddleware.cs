using System;
using System.Threading.Tasks;
using HMS.API.Application.Common;
using HMS.API.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HMS.API.Middleware
{
    // Middleware to enforce an on-premise tenant configuration. If configured, this middleware
    // will set CurrentTenantAccessor.CurrentTenantId for the duration of the request so
    // EF query filters and auth flows are scoped to the configured tenant.
    public class OnPremiseTenantMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly bool _enabled;
        private readonly IConfiguration _cfg;

        public OnPremiseTenantMiddleware(RequestDelegate next, IConfiguration cfg)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _cfg = cfg;
            _enabled = _cfg.GetValue<bool?>("OnPremise:Enabled") ?? false;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            Guid? tenantToSet = null;

            if (!_enabled)
            {
                await _next(context);
                return;
            }

            // First consult configuration value if present
            if (Guid.TryParse(_cfg["OnPremise:TenantId"], out var cfgTid))
            {
                tenantToSet = cfgTid;
            }
            else
            {
                // Try read persisted AppSettings value from DB
                try
                {
                    var db = context.RequestServices.GetService(typeof(AuthDbContext)) as AuthDbContext;
                    if (db != null)
                    {
                        var s = await db.AppSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Key == "OnPremise:TenantId");
                        if (s != null && Guid.TryParse(s.Value, out var persisted)) tenantToSet = persisted;
                        else
                        {
                            // Fallback to tenant code resolution
                            var code = _cfg["OnPremise:TenantCode"];
                            if (string.IsNullOrWhiteSpace(code))
                            {
                                var s2 = await db.AppSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Key == "OnPremise:TenantCode");
                                if (s2 != null) code = s2.Value;
                            }

                            if (!string.IsNullOrWhiteSpace(code))
                            {
                                var t = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Code == code);
                                if (t != null) tenantToSet = t.Id;
                            }
                        }
                    }
                }
                catch
                {
                    // ignore resolution errors
                }
            }

            if (tenantToSet.HasValue)
            {
                CurrentTenantAccessor.CurrentTenantId = tenantToSet;
            }

            try
            {
                await _next(context);
            }
            finally
            {
                if (tenantToSet.HasValue) CurrentTenantAccessor.Clear();
            }
        }
    }
}
