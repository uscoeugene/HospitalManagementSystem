using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using HMS.UI.Services;
using Microsoft.Extensions.Configuration;

namespace HMS.UI.ViewComponents
{
    public class DeploymentModeViewComponent : ViewComponent
    {
        private readonly ApiClient _api;
        private readonly IConfiguration _config;

        public DeploymentModeViewComponent(ApiClient api, IConfiguration config)
        {
            _api = api;
            _config = config;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            string mode = "Unknown";

            try
            {
                // Try read DB-backed app setting "System:DeploymentMode" via API
                var res = await _api.GetAsync<object>("/appsettings/System:DeploymentMode");
                if (res is JsonElement je)
                {
                    if (je.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.String)
                    {
                        mode = v.GetString() ?? "Unknown";
                    }
                    else if (je.ValueKind == JsonValueKind.String)
                    {
                        mode = je.GetString() ?? "Unknown";
                    }
                }
                else if (res != null)
                {
                    mode = res.ToString() ?? "Unknown";
                }
            }
            catch
            {
                // swallow - mode remains Unknown
            }

            // If API didn't provide mode, fall back to local configuration
            if (string.IsNullOrWhiteSpace(mode) || mode == "Unknown")
            {
                try
                {
                    var cfg = _config["Deployment:Mode"];
                    if (!string.IsNullOrWhiteSpace(cfg)) mode = cfg;
                }
                catch { }
            }

            // normalize common values
            if (!string.IsNullOrWhiteSpace(mode))
            {
                mode = mode.Trim();
                if (mode.Equals("OnPrem", System.StringComparison.OrdinalIgnoreCase) || mode.Equals("OnPremise", System.StringComparison.OrdinalIgnoreCase)) mode = "OnPrem";
                if (mode.Equals("Online", System.StringComparison.OrdinalIgnoreCase)) mode = "Online";
            }

            return View("Default", mode);
        }
    }
}
