using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using HMS.API.Application.Auth.DTOs;
using HMS.API.Application.Billing.DTOs;
using HMS.API.Application.Payments.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HMS.API.IntegrationTests
{
    public class PaymentsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public PaymentsIntegrationTests(CustomWebApplicationFactory factory)
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
        public async Task Payment_Create_And_Receipt_And_Refund_Flow()
        {
            var client = _factory.CreateClient();
            var token = await LoginAsAdmin(client);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // create patient via Patients API
            var patientReq = new HMS.API.Application.Patient.DTOs.RegisterPatientRequest { FirstName = "John", LastName = "Test", DateOfBirth = System.DateTimeOffset.UtcNow.AddYears(-30) };
            var patientResp = await client.PostAsJsonAsync("/patients", patientReq);
            patientResp.IsSuccessStatusCode.Should().BeTrue();
            var patient = await patientResp.Content.ReadFromJsonAsync<HMS.API.Application.Patient.DTOs.PatientResponse>();

            // create invoice for patient via billing
            var invoiceReq = new CreateInvoiceRequest { PatientId = patient!.Id, Items = new System.Collections.Generic.List<CreateInvoiceItemRequest> { new CreateInvoiceItemRequest { Description = "Test service", UnitPrice = 100, Quantity = 1 } } };
            var invResp = await client.PostAsJsonAsync("/billing", invoiceReq);
            invResp.IsSuccessStatusCode.Should().BeTrue();
            var inv = await invResp.Content.ReadFromJsonAsync<InvoiceDto>();

            // create payment
            var payReq = new CreatePaymentRequest { InvoiceId = inv!.Id, PatientId = inv.PatientId, Amount = inv.TotalAmount };
            var payResp = await client.PostAsJsonAsync("/payments", payReq);
            payResp.IsSuccessStatusCode.Should().BeTrue();
            var payment = await payResp.Content.ReadFromJsonAsync<PaymentDto>();
            payment.Should().NotBeNull();

            // get receipt
            var receiptResp = await client.GetAsync($"/payments/{payment!.Id}/receipt");
            receiptResp.IsSuccessStatusCode.Should().BeTrue();

            // refund
            var refundReq = new RefundRequest { Amount = payment.Amount, Reason = "Test refund" };
            var refundResp = await client.PostAsJsonAsync($"/payments/{payment.Id}/refund", refundReq);
            refundResp.IsSuccessStatusCode.Should().BeTrue();
        }
    }
}}