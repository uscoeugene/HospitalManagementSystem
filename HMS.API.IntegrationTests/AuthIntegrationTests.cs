using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using HMS.API.Application.Auth.DTOs;
using HMS.API.Application.Patient.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HMS.API.IntegrationTests
{
    public class AuthIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public AuthIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<string> LoginAsAdmin(HttpClient client)
        {
            var login = new LoginRequest { Username = "admin", Password = "Admin@12345" };
            var loginResp = await client.PostAsJsonAsync("/auth/login", login);
            loginResp.IsSuccessStatusCode.Should().BeTrue();
            var body = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
            return body!.AccessToken;
        }

        [Fact]
        public async Task Patient_Flows_As_Admin()
        {
            var client = _factory.CreateClient();

            var token = await LoginAsAdmin(client);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // create patient
            var create = new RegisterPatientRequest { FirstName = "John", LastName = "Doe", DateOfBirth = System.DateTimeOffset.UtcNow.AddYears(-30) };
            var createResp = await client.PostAsJsonAsync("/patients", create);
            createResp.IsSuccessStatusCode.Should().BeTrue();

            var created = await createResp.Content.ReadFromJsonAsync<PatientResponse>();
            created.Should().NotBeNull();

            // list patients
            var listResp = await client.GetAsync("/patients");
            listResp.IsSuccessStatusCode.Should().BeTrue();

            var listBody = await listResp.Content.ReadFromJsonAsync<dynamic>();
            ((int)listBody.totalCount).Should().BeGreaterThan(0);

            // search
            var searchResp = await client.GetAsync("/patients?q=John");
            searchResp.IsSuccessStatusCode.Should().BeTrue();

            var searchBody = await searchResp.Content.ReadFromJsonAsync<dynamic>();
            ((int)searchBody.totalCount).Should().BeGreaterThanOrEqualTo(1);

            // add visit
            var visitReq = new AddVisitRequest { VisitAt = System.DateTimeOffset.UtcNow, VisitType = "outpatient", Notes = "Test visit" };
            var visitResp = await client.PostAsJsonAsync($"/patients/{created!.Id}/visits", visitReq);
            visitResp.IsSuccessStatusCode.Should().BeTrue();

            var visitBody = await visitResp.Content.ReadFromJsonAsync<VisitResponse>();
            visitBody.Should().NotBeNull();
        }

        [Fact]
        public async Task Patient_Create_Unauthorized_For_No_Permission()
        {
            var client = _factory.CreateClient();

            // do not auth
            var create = new RegisterPatientRequest { FirstName = "Jane", LastName = "Roe", DateOfBirth = System.DateTimeOffset.UtcNow.AddYears(-25) };
            var createResp = await client.PostAsJsonAsync("/patients", create);
            createResp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        }
    }
}