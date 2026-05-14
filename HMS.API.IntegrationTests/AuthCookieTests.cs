using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using HMS.API.Application.Auth.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HMS.API.IntegrationTests
{
    public class AuthCookieTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public AuthCookieTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Login_Sets_HmsAuth_Cookie_And_Cookie_Authenticates()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var login = new LoginRequest { Username = "admin", Password = "Admin@12345" };
            var loginResp = await client.PostAsJsonAsync("/auth/login", login);
            loginResp.IsSuccessStatusCode.Should().BeTrue();

            // Ensure cookie set
            loginResp.Headers.Contains("Set-Cookie").Should().BeTrue();

            // Extract cookie value
            var setCookie = loginResp.Headers.GetValues("Set-Cookie");
            string? cookieHeader = null;
            foreach (var v in setCookie)
            {
                if (v.StartsWith("HmsAuth=")) { cookieHeader = v; break; }
            }

            cookieHeader.Should().NotBeNull();

            // Send request with cookie and access protected endpoint
            var req = new HttpRequestMessage(HttpMethod.Get, "/patients");
            req.Headers.Add("Cookie", cookieHeader);

            var resp = await client.SendAsync(req);
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task LocalToken_Issued_By_LocalAuth_Controller_Is_Validated_By_JwtHandler()
        {
            var client = _factory.CreateClient();

            // Register a local user via LocalAuthController
            var uname = "localuser" + System.Guid.NewGuid().ToString("N").Substring(0, 6);
            var reg = new RegisterRequest { Username = uname, Password = "Pass@12345", Email = uname + "@example.com" };

            var regResp = await client.PostAsJsonAsync("/localauth/register", reg);
            regResp.EnsureSuccessStatusCode();

            // Login local
            var loginReq = new LoginRequest { Username = uname, Password = "Pass@12345" };
            var loginResp = await client.PostAsJsonAsync("/localauth/login", loginReq);
            loginResp.EnsureSuccessStatusCode();

            var body = await loginResp.Content.ReadFromJsonAsync<dynamic>();
            string accessToken = body.accessToken;

            // Use token in Authorization header to call protected endpoint
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var resp = await client.GetAsync("/patients");
            // No tenant header -> local user likely has null tenant; endpoint requires auth so should be 200 or 403 depending on permissions
            resp.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        }
    }
}
