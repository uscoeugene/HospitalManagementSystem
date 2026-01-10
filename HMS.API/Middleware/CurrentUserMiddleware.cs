using System.Threading.Tasks;
using HMS.API.Application.Common;
using Microsoft.AspNetCore.Http;

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
            // The CurrentUserService reads from HttpContext on demand, so nothing to do here currently.
            await _next(context);
        }
    }
}