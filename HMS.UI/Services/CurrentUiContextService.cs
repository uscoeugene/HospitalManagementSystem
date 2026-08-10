using System;
using Microsoft.AspNetCore.Http;

namespace HMS.UI.Services
{
    public class CurrentUiContextService : ICurrentUiContextService
    {
        private readonly IHttpContextAccessor _ctx;

        public CurrentUiContextService(IHttpContextAccessor ctx)
        {
            _ctx = ctx;
        }

        public Guid? GetCurrentTenantId()
        {
            try
            {
                var cookie = _ctx.HttpContext?.Request?.Cookies["HmsTenantId"];
                if (Guid.TryParse(cookie, out var tid)) return tid;
            }
            catch (Exception ex) { try { System.Diagnostics.Trace.TraceError(ex.ToString()); } catch { } }
            return null;
        }
    }
}
