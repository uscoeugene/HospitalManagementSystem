using System;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace HMS.API.IntegrationTests
{
    public class NotificationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public NotificationIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<string> LoginAsAdmin(HttpClient client)
        {
            var login = new HMS.API.Application.Auth.DTOs.LoginRequest { Username = "admin", Password = "Admin@12345" };
            var loginResp = await client.PostAsJsonAsync("/auth/login", login);
            loginResp.IsSuccessStatusCode.Should().BeTrue();
            var body = await loginResp.Content.ReadFromJsonAsync<HMS.API.Application.Auth.DTOs.LoginResponse>();
            return body!.AccessToken;
        }

        [Fact]
        public async Task SubscriptionsEndpoint_ReturnsPatientGroup()
        {
            var client = _factory.CreateClient();
            var token = await LoginAsAdmin(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var resp = await client.GetAsync("/notifications/subscriptions");
            resp.IsSuccessStatusCode.Should().BeTrue();
            var channels = await resp.Content.ReadFromJsonAsync<string[]>();
            channels.Should().NotBeNull();
            channels!.Length.Should().BeGreaterThan(0);
            channels.Should().Contain(c => c.StartsWith("patient-"));
        }

        [Fact]
        public async Task GroupLevelNotification_Received_WhenSubscribedToPharmacyGroup()
        {
            var client = _factory.CreateClient();
            var token = await LoginAsAdmin(client);

            var hubConnection = new HubConnectionBuilder().WithUrl(_factory.ClientOptions.BaseAddress + "hubs/notifications", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(token);
            }).WithAutomaticReconnect().Build();

            var tcs = new TaskCompletionSource<string?>();
            hubConnection.On<string, object>("notification", (type, payload) =>
            {
                tcs.TrySetResult(type);
            });

            await hubConnection.StartAsync();
            await hubConnection.InvokeAsync("Subscribe", "pharmacy");

            // create drug and patient and prescription to trigger PrescriptionCharged
            var drug = new HMS.API.Application.Pharmacy.DTOs.DrugDto { Code = "GNP", Name = "GroupNotifyDrug", Price = 1m };
            var drugResp = await client.PostAsJsonAsync("/pharmacy/drugs", drug);
            drugResp.IsSuccessStatusCode.Should().BeTrue();
            var created = await drugResp.Content.ReadFromJsonAsync<HMS.API.Application.Pharmacy.DTOs.DrugDto>();

            var patientReq = new HMS.API.Application.Patient.DTOs.RegisterPatientRequest { FirstName = "Grp", LastName = "Notify", DateOfBirth = DateTimeOffset.UtcNow.AddYears(-30) };
            var patientResp = await client.PostAsJsonAsync("/patients", patientReq);
            patientResp.IsSuccessStatusCode.Should().BeTrue();
            var patient = await patientResp.Content.ReadFromJsonAsync<HMS.API.Application.Patient.DTOs.PatientResponse>();

            var presReq = new HMS.API.Application.Pharmacy.DTOs.CreatePrescriptionRequest { PatientId = patient!.Id, Items = new System.Collections.Generic.List<HMS.API.Application.Pharmacy.DTOs.CreatePrescriptionItem> { new HMS.API.Application.Pharmacy.DTOs.CreatePrescriptionItem { DrugId = created!.Id, Quantity = 1 } } };
            var presResp = await client.PostAsJsonAsync("/pharmacy/prescriptions", presReq);
            presResp.IsSuccessStatusCode.Should().BeTrue();

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            completed.Should().Be(tcs.Task);
            var received = tcs.Task.Result;
            received.Should().NotBeNull();

            await hubConnection.DisposeAsync();
        }
    }
}