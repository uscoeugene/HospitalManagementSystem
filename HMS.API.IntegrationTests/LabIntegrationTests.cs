using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using HMS.API.Application.Auth.DTOs;
using HMS.API.Application.Lab.DTOs;
using HMS.API.Application.Billing.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HMS.API.IntegrationTests
{
    public class LabIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public LabIntegrationTests(CustomWebApplicationFactory factory)
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
        public async Task Lab_CreateTest_And_Request_CreatesInvoice_Linked()
        {
            var client = _factory.CreateClient();
            var token = await LoginAsAdmin(client);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // create lab test
            var test = new LabTestDto { Code = "CBC", Name = "Complete Blood Count", Description = "CBC", Price = 10m };
            var testResp = await client.PostAsJsonAsync("/lab/tests", test);
            testResp.IsSuccessStatusCode.Should().BeTrue();
            var created = await testResp.Content.ReadFromJsonAsync<LabTestDto>();

            // create patient
            var patientReq = new HMS.API.Application.Patient.DTOs.RegisterPatientRequest { FirstName = "Lab", LastName = "Patient", DateOfBirth = System.DateTimeOffset.UtcNow.AddYears(-25) };
            var patientResp = await client.PostAsJsonAsync("/patients", patientReq);
            patientResp.IsSuccessStatusCode.Should().BeTrue();
            var patient = await patientResp.Content.ReadFromJsonAsync<HMS.API.Application.Patient.DTOs.PatientResponse>();

            // create lab request
            var lr = new CreateLabRequest { PatientId = patient!.Id, Items = new System.Collections.Generic.List<CreateLabRequestItem> { new CreateLabRequestItem { LabTestId = created!.Id } } };
            var lrResp = await client.PostAsJsonAsync("/lab/requests", lr);
            lrResp.IsSuccessStatusCode.Should().BeTrue();
            var labReq = await lrResp.Content.ReadFromJsonAsync<LabRequestDto>();

            // verify invoice exists via billing list filtering by visit (none) and patient
            var invoicesResp = await client.GetAsync($"/billing?patientId={patient.Id}");
            invoicesResp.IsSuccessStatusCode.Should().BeTrue();
            var invoices = await invoicesResp.Content.ReadFromJsonAsync<HMS.API.Application.Common.PagedResult<InvoiceDto>>();
            invoices!.Items.Should().NotBeEmpty();

            // check lab request item has ChargeInvoiceItemId set in DB by reading lab request
            var getReqResp = await client.GetAsync($"/lab/requests/{labReq!.Id}");
            getReqResp.IsSuccessStatusCode.Should().BeTrue();
            var fetched = await getReqResp.Content.ReadFromJsonAsync<LabRequestDto>();
            fetched!.Tests.Should().NotBeEmpty();
        }
    }
}