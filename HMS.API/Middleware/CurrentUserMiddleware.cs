using System.Threading.Tasks;
using HMS.API.Application.Common;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace HMS.API.Middleware
{
    public class CurrentUserMiddleware
    {
        private readonly RequestDelegate _next;

        public CurrentUserMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUserService)
        {
            // If the user has tenant claims, populate the CurrentTenantAccessor
            var user = context.User;
            if (user?.Identity != null && user.Identity.IsAuthenticated)
            {
                var tclaim = user.FindFirst("tenant_id")?.Value;
                if (System.Guid.TryParse(tclaim, out var tid))
                {
                    CurrentTenantAccessor.CurrentTenantId = tid;
                }
            }

            // The CurrentUserService reads from HttpContext on demand, so nothing else to do
            await _next(context);

            // clear after request
            CurrentTenantAccessor.Clear();
        }
    }
}