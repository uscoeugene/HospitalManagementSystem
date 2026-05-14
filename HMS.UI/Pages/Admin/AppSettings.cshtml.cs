using System.Threading.Tasks;
using HMS.UI.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;

namespace HMS.UI.Pages.Admin
{
    public class AppSettingsModel : PageModel
    {
        private readonly ApiClient _api;
        public AppSettingsModel(ApiClient api) { _api = api; }

        [BindProperty]
        public string Key { get; set; } = string.Empty;
        [BindProperty]
        public string Value { get; set; } = string.Empty;

        public System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string,string>> Items { get; set; } = new();

        public async Task OnGetAsync()
        {
            // Call list endpoint to retrieve settings
            var list = await _api.GetAsync<System.Collections.Generic.List<System.Text.Json.JsonElement>>("/appsettings");
            Items = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>();
            if (list != null)
            {
                foreach (var el in list)
                {
                    if (el.TryGetProperty("key", out var k) && el.TryGetProperty("value", out var v))
                    {
                        Items.Add(new System.Collections.Generic.KeyValuePair<string, string>(k.GetString() ?? string.Empty, v.GetString() ?? string.Empty));
                    }
                }
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Key)) { ModelState.AddModelError(string.Empty, "Key required"); await OnGetAsync(); return Page(); }
            await _api.PostAsync<object>("/appsettings/upsert", new { Key = Key, Value = Value });
            TempData["Success"] = "Setting saved";
            return RedirectToPage();
        }
    }
}
