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
                var tid = context.Request.Headers["X-Tenant-Id"].ToString();
                if (Guid.TryParse(tid, out var g))
                {
                    CurrentTenantAccessor.CurrentTenantId = g;
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