using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HMS.UI.Security
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class HasPermissionAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _permission;

        public HasPermissionAttribute(string permission)
        {
            _permission = permission;
        }

        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (user?.Identity == null || !user.Identity.IsAuthenticated)
            {
                // Redirect to login page when not authenticated (ChallengeResult requires auth schemes)
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return Task.CompletedTask;
            }

            var ok = user.HasClaim(c => string.Equals(c.Type, "permission", StringComparison.OrdinalIgnoreCase) && string.Equals(c.Value, _permission, StringComparison.OrdinalIgnoreCase));
            if (!ok)
            {
                // Redirect to friendly 403 error page when lacking permission
                context.Result = new RedirectResult("/Error/403");
            }

            return Task.CompletedTask;
        }
    }
}
