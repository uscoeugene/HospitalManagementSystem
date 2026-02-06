using System;
using System.Threading.Tasks;
using HMS.API.Application.Billing.DTOs;
using HMS.API.Application.Common;
using System.Collections.Generic;

namespace HMS.API.Application.Billing
{
    public interface IBillingService
    {
        Task<InvoiceDto> CreateInvoiceAsync(CreateInvoiceRequest request);
        Task<InvoiceDto?> GetInvoiceAsync(Guid id);
        Task<InvoiceDto> ApplyPaymentAsync(Guid invoiceId, ApplyPaymentRequest request);

        Task<PagedResult<InvoiceDto>> ListInvoicesAsync(Guid? patientId = null, Guid? visitId = null, string? status = null, int page = 1, int pageSize = 20);

        Task<PagedResult<InvoicePaymentDto>> ListPaymentsAsync(Guid? invoiceId = null, Guid? patientId = null, int page = 1, int pageSize = 20);

        Task<InvoiceDto> CreateInvoiceFromLabRequestAsync(CreateInvoiceFromLabRequest request);

        // Debt APIs
        Task<IEnumerable<DTOs.DebtDto>> ListDebtsAsync(Guid? patientId = null, bool unresolvedOnly = true);
        Task<PagedResult<DTOs.DebtDto>> ListDebtsPagedAsync(Guid? invoiceId = null, Guid? patientId = null, bool unresolvedOnly = true, int page = 1, int pageSize = 20);
        Task ResolveDebtAsync(Guid debtId);
        Task PayDebtAsync(Guid debtId, decimal amount, string? externalReference = null);
        Task<IEnumerable<DTOs.PaymentResultDto>> PayMultipleDebtsAsync(IEnumerable<DTOs.PayDebtRequest> requests);

        // Reporting
        Task<IEnumerable<DTOs.DebtAgingDto>> GetDebtAgingReportAsync(int daysBucket = 30);
        Task<IEnumerable<DTOs.OutstandingByPatientDto>> GetOutstandingByPatientAsync();
    }

    public class CreateInvoiceFromLabRequest
    {
        public Guid PatientId { get; set; }
        public Guid? VisitId { get; set; }
        public CreateInvoiceItemRequest[] Items { get; set; } = Array.Empty<CreateInvoiceItemRequest>();
        public string Currency { get; set; } = "USD";
    }
}