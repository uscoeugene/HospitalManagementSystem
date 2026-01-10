using System;
using System.Threading.Tasks;
using HMS.API.Application.Billing.DTOs;
using HMS.API.Application.Common;

namespace HMS.API.Application.Billing
{
    public interface IBillingService
    {
        Task<InvoiceDto> CreateInvoiceAsync(CreateInvoiceRequest request);
        Task<InvoiceDto?> GetInvoiceAsync(Guid id);
        Task<InvoiceDto> ApplyPaymentAsync(Guid invoiceId, ApplyPaymentRequest request);

        Task<PagedResult<InvoiceDto>> ListInvoicesAsync(Guid? patientId = null, Guid? visitId = null, string? status = null, int page = 1, int pageSize = 20);

        Task<PagedResult<InvoicePaymentDto>> ListPaymentsAsync(Guid? invoiceId = null, Guid? patientId = null, int page = 1, int pageSize = 20);
    }
}