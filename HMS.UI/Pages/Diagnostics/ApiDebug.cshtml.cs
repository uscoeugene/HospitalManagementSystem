using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using HMS.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace HMS.UI.Pages.Diagnostics
{
    public class ApiDebugModel : PageModel
    {
        private readonly ApiClient _api;

        public ApiClient.ApiClientDebugInfo? DebugInfo { get; private set; }

        public ApiDebugModel(ApiClient api)
        {
            _api = api;
        }

        public IActionResult OnGet()
        {
            DebugInfo = _api.GetLastDebug();
            return Page();
        }
    }
}
