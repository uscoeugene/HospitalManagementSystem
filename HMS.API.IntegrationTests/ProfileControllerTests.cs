using System;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HMS.API.IntegrationTests
{
    public class ProfileControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public ProfileControllerTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<string> LoginAs(string username, string password, System.Net.Http.HttpClient client)
        {
            var login = new HMS.API.Application.Auth.DTOs.LoginRequest { Username = username, Password = password };
            var resp = await client.PostAsJsonAsync("/auth/login", login);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadFromJsonAsync<HMS.API.Application.Auth.DTOs.LoginResponse>();
            return body!.AccessToken;
        }

        [Fact]
        public async Task GetMyProfile_Returns_EmptyOrNotFound_For_NewUser()
        {
            var client = _factory.CreateClient();
            var token = await LoginAs("user", "User@12345", client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var resp = await client.GetAsync("/api/profile/me");
            // may be NotFound if profile not created yet
            resp.StatusCode.Should().BeOneOf(System.Net.HttpStatusCode.OK, System.Net.HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Admin_Can_Get_Other_User_Profile()
        {
            var client = _factory.CreateClient();
            var token = await LoginAs("admin", "Admin@12345", client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // get user id for 'user' by logging in and reading token claims
            var userClient = _factory.CreateClient();
            var userToken = await LoginAs("user", "User@12345", userClient);

            // Try to access the user profile as admin (should be allowed)
            var userId = ExtractUserIdFromToken(userToken);
            var resp = await client.GetAsync($"/api/profile/{userId}");
            resp.StatusCode.Should().BeOneOf(System.Net.HttpStatusCode.OK, System.Net.HttpStatusCode.NotFound);
        }

        private Guid ExtractUserIdFromToken(string token)
        {
            // token is a JWT; parse payload part (unsafe but acceptable for tests) to get user id claim
            var parts = token.Split('.');
            if (parts.Length < 2) return Guid.Empty;
            var payload = parts[1];
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(PadBase64(payload)));
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("UserId", out var uid) && uid.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                if (Guid.TryParse(uid.GetString(), out var g)) return g;
            }

            if (doc.RootElement.TryGetProperty("userId", out var uid2) && uid2.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                if (Guid.TryParse(uid2.GetString(), out var g)) return g;
            }

            if (doc.RootElement.TryGetProperty("sub", out var sub) && sub.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                if (Guid.TryParse(sub.GetString(), out var g)) return g;
            }

            return Guid.Empty;
        }

        private static string PadBase64(string s)
        {
            switch (s.Length % 4)
            {
                case 2: return s + "==";
                case 3: return s + "=";
                default: return s;
            }
        }
    }
}
