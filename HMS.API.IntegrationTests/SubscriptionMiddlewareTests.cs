using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HMS.API.Application.Common;
using HMS.API.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HMS.API.IntegrationTests
{
    public class SubscriptionMiddlewareTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public SubscriptionMiddlewareTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Requests_blocked_for_inactive_subscription()
        {
            using var scope = _factory.Services.CreateScope();
            var authDb = scope.ServiceProvider.GetRequiredService<HMS.API.Infrastructure.Auth.AuthDbContext>();
            var tenant = await authDb.Tenants.SingleOrDefaultAsync(t => t.Code == "GVC") ?? throw new InvalidOperationException("Tenant not found");

            var sub = await authDb.Set<TenantSubscription>().SingleOrDefaultAsync(s => s.TenantId == tenant.Id);
            sub.Status = SubscriptionStatus.Cancelled;
            await authDb.SaveChangesAsync();

            // call a protected endpoint (patients list) with X-Tenant-Id header which triggers middleware scoping
            var req = new HttpRequestMessage(HttpMethod.Get, "/patients");
            req.Headers.Add("X-Tenant-Id", tenant.Id.ToString());

            var resp = await _client.SendAsync(req);
            Assert.Equal((System.Net.HttpStatusCode)402, resp.StatusCode);
        }

        [Fact]
        public async Task Webhook_updates_subscription_and_allows_requests()
        {
            using var scope = _factory.Services.CreateScope();
            var authDb = scope.ServiceProvider.GetRequiredService<HMS.API.Infrastructure.Auth.AuthDbContext>();
            var tenant = await authDb.Tenants.SingleOrDefaultAsync(t => t.Code == "GVC") ?? throw new InvalidOperationException("Tenant not found");

            var sub = await authDb.Set<TenantSubscription>().SingleOrDefaultAsync(s => s.TenantId == tenant.Id);
            sub.Status = SubscriptionStatus.Cancelled;
            await authDb.SaveChangesAsync();

            var payload = JsonSerializer.Serialize(new { tenantId = tenant.Id, status = "Active", endAt = DateTimeOffset.UtcNow.AddMonths(3) });
            var dto = new { EventType = "subscription.updated", Payload = payload };
            var json = JsonSerializer.Serialize(dto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // compute signature using test-secret (see CustomWebApplicationFactory)
            var secret = Encoding.UTF8.GetBytes("test-secret");
            using var hmac = new System.Security.Cryptography.HMACSHA256(secret);
            var sig = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(json)));

            var request = new HttpRequestMessage(HttpMethod.Post, "/subscriptions/webhook")
            {
                Content = content
            };
            request.Headers.Add("Billing-Signature", sig);

            var resp = await _client.SendAsync(request);
            Assert.Equal(System.Net.HttpStatusCode.Accepted, resp.StatusCode);

            var updated = await authDb.Set<TenantSubscription>().SingleOrDefaultAsync(s => s.TenantId == tenant.Id);
            Assert.Equal(SubscriptionStatus.Active, updated.Status);

            // call protected endpoint with X-Tenant-Id header -> should not be Payment Required
            var req = new HttpRequestMessage(HttpMethod.Get, "/patients");
            req.Headers.Add("X-Tenant-Id", tenant.Id.ToString());

            var patientsResp = await _client.SendAsync(req);
            Assert.NotEqual((System.Net.HttpStatusCode)402, patientsResp.StatusCode);
        }
    }
}
