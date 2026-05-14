using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using HMS.UI.Services;

namespace HMS.UI.Middleware
{
    public class AutoRefreshMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceProvider _services;

        public AutoRefreshMiddleware(RequestDelegate next, IServiceProvider services)
        {
            _next = next;
            _services = services;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Pass through
            await _next(context);

            // If response already started we cannot modify it
            if (context.Response.HasStarted) return;

            // Only attempt on 401 responses
            if (context.Response.StatusCode != StatusCodes.Status401Unauthorized) return;

            // Avoid retry loops
            if (context.Request.Headers.ContainsKey("X-Refresh-Attempt")) return;

            // Don't attempt refresh for auth endpoints
            var path = context.Request.Path;
            if (path.StartsWithSegments("/auth", System.StringComparison.OrdinalIgnoreCase) || path.StartsWithSegments("/Account", System.StringComparison.OrdinalIgnoreCase))
                return;

            // Resolve RefreshService from a created scope so scoped dependencies are available
            try
            {
                using var scope = _services.CreateScope();
                var refresh = scope.ServiceProvider.GetService<RefreshService>();
                if (refresh == null) return;

                var ok = await refresh.TryRefreshAsync();
                if (!ok) return;

                // Mark attempt and redirect back to the same URL so browser retries with refreshed cookie
                context.Response.Redirect(context.Request.Path + context.Request.QueryString);
            }
            catch
            {
                // swallow and continue
            }
        }
    }
}
