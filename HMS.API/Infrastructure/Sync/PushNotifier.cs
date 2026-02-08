using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using HMS.API.Infrastructure.Auth;
using HMS.API.Domain.Common;
using System.Security.Cryptography;

namespace HMS.API.Infrastructure.Sync
{
    public class PushNotifier
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<PushNotifier> _logger;

        public PushNotifier(IServiceProvider services, ILogger<PushNotifier> logger)
        {
            _services = services;
            _logger = logger;
        }

        public async Task NotifySubscriptionChangedAsync(Guid tenantId, object payload)
        {
            using var scope = _services.CreateScope();
            var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var nodes = authDb.Set<TenantNode>().Where(n => n.TenantId == tenantId && n.IsActive).ToList();
            if (!nodes.Any()) return;

            var httpFactory = scope.ServiceProvider.GetService<IHttpClientFactory>();

            foreach (var node in nodes)
            {
                try
                {
                    var json = JsonSerializer.Serialize(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var client = (httpFactory ?? scope.ServiceProvider.GetService<IHttpClientFactory>())?.CreateClient() ?? new HttpClient();

                    // compute signature if CallbackSecret present (base64)
                    if (!string.IsNullOrWhiteSpace(node.CallbackSecret))
                    {
                        try
                        {
                            var secret = Convert.FromBase64String(node.CallbackSecret);
                            using var hmac = new HMACSHA256(secret);
                            var sig = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(json)));
                            client.DefaultRequestHeaders.Remove("X-Central-Signature");
                            client.DefaultRequestHeaders.Add("X-Central-Signature", sig);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Invalid callback secret for node {Node}", node.CallbackUrl);
                        }
                    }

                    var resp = await client.PostAsync(node.CallbackUrl, content);
                    if (!resp.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Push to node {Node} returned {Status}", node.CallbackUrl, resp.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to push to node {Node}", node.CallbackUrl);
                }
            }
        }
    }
}
