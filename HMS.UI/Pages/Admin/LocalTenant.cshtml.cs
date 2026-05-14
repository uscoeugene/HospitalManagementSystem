using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using HMS.UI.Services;
using System.Net.Http.Json;

namespace HMS.UI.Pages.Admin
{
    public class LocalTenantModel : PageModel
    {
        private readonly ApiClient _api;

        public LocalTenantModel(ApiClient api)
        {
            _api = api;
        }

        [BindProperty]
        public Guid? SelectedTenantId { get; set; }

        public object? LocalDefault { get; set; }

        public System.Collections.Generic.List<HMS.UI.Models.TenantItem> Tenants { get; set; } = new();

        public async Task OnGetAsync()
        {
            // Load current local-default setting
            LocalDefault = await _api.GetAsync<object>("/tenants/local-default");

            // Load tenants for dropdown
            var t = await _api.GetAsync<HMS.UI.Models.TenantItem[]>("/tenants");
            if (t != null) Tenants = new System.Collections.Generic.List<HMS.UI.Models.TenantItem>(t);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!SelectedTenantId.HasValue)
            {
                ModelState.AddModelError(string.Empty, "Please select a tenant");
                TempData["Error"] = "Please select a tenant";
                await OnGetAsync();
                return Page();
            }

            // Persist to AppSettings table via API so middleware and UI read the persisted value
            var r1 = await _api.PostRawAsync("/appsettings/upsert", new { Key = "OnPremise:TenantId", Value = SelectedTenantId.Value.ToString() });
            if (!r1.IsSuccessStatusCode)
            {
                var err = await r1.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, "Failed to persist TenantId: " + err);
                await OnGetAsync();
                return Page();
            }

            // Also set tenant code/name in AppSettings for human readability
            var selected = Tenants.Find(t => t.Id == SelectedTenantId.Value);
            if (selected != null)
            {
                var r2 = await _api.PostRawAsync("/appsettings/upsert", new { Key = "OnPremise:TenantCode", Value = selected.Code });
                var r3 = await _api.PostRawAsync("/appsettings/upsert", new { Key = "OnPremise:TenantName", Value = selected.Name });
                if (!r2.IsSuccessStatusCode || !r3.IsSuccessStatusCode)
                {
                    var err = (!r2.IsSuccessStatusCode) ? await r2.Content.ReadAsStringAsync() : await r3.Content.ReadAsStringAsync();
                    ModelState.AddModelError(string.Empty, "Failed to persist tenant metadata: " + err);
                    await OnGetAsync();
                    return Page();
                }
            }

            // call tenant API to set local default too
            var r4 = await _api.PostRawAsync($"/tenants/{SelectedTenantId.Value}/set-local-default", new { });
            if (!r4.IsSuccessStatusCode)
            {
                var err = await r4.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, "Failed to set local default tenant: " + err);
                await OnGetAsync();
                return Page();
            }
            TempData["Success"] = "Local default tenant updated";
            return RedirectToPage();
        }
    }
}
