using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using HMS.API.Application.Auth.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HMS.API.IntegrationTests
{
    public class PermissionTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public PermissionTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<string> RegisterAndLoginNonCashier(HttpClient client)
        {
            var username = "regularuser" + System.Guid.NewGuid().ToString("N").Substring(0, 6);
            var register = new RegisterRequest { Username = username, Password = "Pass@12345", Email = username + "@example.com" };
            var regResp = await client.PostAsJsonAsync("/auth/register", register);
            regResp.IsSuccessStatusCode.Should().BeTrue();

            var login = new LoginRequest { Username = username, Password = "Pass@12345" };
            var loginResp = await client.PostAsJsonAsync("/auth/login", login);
            loginResp.IsSuccessStatusCode.Should().BeTrue();
            var body = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
            return body!.AccessToken;
        }

        [Fact]
        public async Task Payments_Create_RequiresAuthentication()
        {
            var client = _factory.CreateClient();
            var payReq = new HMS.API.Application.Payments.DTOs.CreatePaymentRequest { InvoiceId = System.Guid.NewGuid(), PatientId = System.Guid.NewGuid(), Amount = 10m };
            var resp = await client.PostAsJsonAsync("/payments", payReq);
            resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Payments_Create_RequiresCashierRole()
        {
            var client = _factory.CreateClient();
            // login as admin but remove cashier role? For now admin has permission so emulate non-privileged by not logging in
            var payReq = new HMS.API.Application.Payments.DTOs.CreatePaymentRequest { InvoiceId = System.Guid.NewGuid(), PatientId = System.Guid.NewGuid(), Amount = 10m };
            var resp = await client.PostAsJsonAsync("/payments", payReq);
            resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Payments_Create_RequiresCashierRole_ReturnsForbidden()
        {
            var client = _factory.CreateClient();
            var token = await RegisterAndLoginNonCashier(client);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var payReq = new HMS.API.Application.Payments.DTOs.CreatePaymentRequest { InvoiceId = System.Guid.NewGuid(), PatientId = System.Guid.NewGuid(), Amount = 10m };
            var resp = await client.PostAsJsonAsync("/payments", payReq);
            resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        }
    }
}