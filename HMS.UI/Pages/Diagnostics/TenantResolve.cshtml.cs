using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using HMS.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace HMS.UI.Pages.Diagnostics
{
    public class TenantResolveModel : PageModel
    {
        private readonly ApiClient _api;

        public object? Result { get; private set; }

        public TenantResolveModel(ApiClient api)
        {
            _api = api;
        }

        public async Task<IActionResult> OnGet()
        {
            // Call API diagnostics endpoint via ApiClient (server-side) so Host header propagation occurs
            try
            {
                Result = await _api.GetAsync<object>("/tenants/diagnostics/tenant-resolve");
            }
            catch (System.Exception ex)
            {
                Result = new { error = ex.Message };
            }

            return Page();
        }
    }
}
