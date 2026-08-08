using Microsoft.AspNetCore.Mvc.Filters;
using HMS.UI.Services;

namespace HMS.UI.Filters
{
    public class CurrentUiContextViewDataFilter : IActionFilter
    {
        private readonly ICurrentUiContextService _svc;

        public CurrentUiContextViewDataFilter(ICurrentUiContextService svc)
        {
            _svc = svc;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            var tenant = _svc.GetCurrentTenantId();
            if (context.Controller is Microsoft.AspNetCore.Mvc.Controller ctrl)
            {
                ctrl.ViewData["CurrentTenantId"] = tenant;
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
