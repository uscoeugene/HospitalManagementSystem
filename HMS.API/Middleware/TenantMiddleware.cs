using System;
using System.Threading.Tasks;
using HMS.API.Application.Common;
using Microsoft.AspNetCore.Http;

namespace HMS.API.Middleware
{
    // Middleware to extract tenant information from incoming requests.
    // Expected headers:
    // - X-Tenant-Id (optional GUID)
    public class TenantMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                var logger = context.RequestServices.GetService(typeof(Microsoft.Extensions.Logging.ILogger<TenantMiddleware>)) as Microsoft.Extensions.Logging.ILogger<TenantMiddleware>;

                var tid = context.Request.Headers["X-Tenant-Id"].ToString();
                if (Guid.TryParse(tid, out var g))
                {
                    CurrentTenantAccessor.CurrentTenantId = g;
                    logger?.LogDebug("TenantMiddleware: X-Tenant-Id header set current tenant {TenantId}", g);
                }
                else if (!string.IsNullOrWhiteSpace(tid))
                {
                    logger?.LogWarning("TenantMiddleware: invalid X-Tenant-Id header value: {Value}", tid);
                }
            }
            catch { }

            try
            {
                await _next(context);
            }
            finally
            {
                CurrentTenantAccessor.Clear();
            }
        }
    }
}