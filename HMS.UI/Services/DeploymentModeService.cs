using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HMS.UI.Services
{
    public class DeploymentModeService : IDeploymentModeService
    {
        private readonly ApiClient _api;
        private readonly IConfiguration _config;
        private readonly ILogger<DeploymentModeService> _logger;

        public DeploymentModeService(ApiClient api, IConfiguration config, ILogger<DeploymentModeService> logger)
        {
            _api = api;
            _config = config;
            _logger = logger;
        }

        public async Task<string> GetEffectiveModeAsync()
        {
            try
            {
                var health = await _api.GetAsync<JsonElement>("/admin/health/appsettings");
                if (health.ValueKind == JsonValueKind.Object &&
                    health.TryGetProperty("deploymentMode", out var deploymentMode) &&
                    deploymentMode.ValueKind == JsonValueKind.String)
                {
                    var value = deploymentMode.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return Normalize(value);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to resolve deployment mode from API health endpoint");
            }

            var mode = _config["Deployment:Mode"] ?? _config["System:DeploymentMode"];
            if (!string.IsNullOrWhiteSpace(mode))
            {
                return Normalize(mode);
            }

            return "Unknown";
        }

        private static string Normalize(string mode)
        {
            var trimmed = mode.Trim();
            if (trimmed.Equals("OnPremise", StringComparison.OrdinalIgnoreCase))
            {
                return "OnPrem";
            }

            return trimmed;
        }
    }
}
