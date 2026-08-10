using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HMS.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HMS.UI.Pages.Admin
{
    public class AppSettingsModel : PageModel
    {
        private readonly ApiClient _api;
        private readonly IDeploymentModeService _deploymentModeService;

        public AppSettingsModel(ApiClient api, IDeploymentModeService deploymentModeService)
        {
            _api = api;
            _deploymentModeService = deploymentModeService;
        }

        [BindProperty]
        public string Key { get; set; } = string.Empty;

        [BindProperty]
        public string Value { get; set; } = string.Empty;

        [BindProperty]
        public string DeploymentMode { get; set; } = "Bootstrap";

        [BindProperty]
        public string PlatformHosts { get; set; } = string.Empty;

        public List<KeyValuePair<string, string>> Items { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadItemsAsync();
            DeploymentMode = await _deploymentModeService.GetEffectiveModeAsync();
            PlatformHosts = GetSettingValue("PlatformContext:Hosts")
                ?? GetSettingValue("PlatformHosts")
                ?? GetSettingValue("PlatformDomains")
                ?? string.Empty;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Key))
            {
                ModelState.AddModelError(string.Empty, "Key required");
                await OnGetAsync();
                return Page();
            }

            await _api.PostAsync<object>("/appsettings/upsert", new { Key, Value });
            TempData["Success"] = "Setting saved";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeploymentModeAsync()
        {
            await _api.PostAsync<object>("/appsettings/upsert", new { Key = "System:DeploymentMode", Value = DeploymentMode });
            TempData["Success"] = "Deployment mode updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostPlatformHostsAsync()
        {
            await _api.PostAsync<object>("/appsettings/upsert", new { Key = "PlatformContext:Hosts", Value = PlatformHosts });
            TempData["Success"] = "Platform host list updated.";
            return RedirectToPage();
        }

        private async Task LoadItemsAsync()
        {
            var list = await _api.GetAsync<List<System.Text.Json.JsonElement>>("/appsettings");
            Items = new List<KeyValuePair<string, string>>();
            if (list == null)
            {
                return;
            }

            foreach (var el in list)
            {
                if (el.TryGetProperty("key", out var k) && el.TryGetProperty("value", out var v))
                {
                    Items.Add(new KeyValuePair<string, string>(k.GetString() ?? string.Empty, v.GetString() ?? string.Empty));
                }
            }
        }

        private string? GetSettingValue(string key)
        {
            return Items.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
        }
    }
}
