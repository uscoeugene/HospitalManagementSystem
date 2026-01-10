using System;
using System.Threading.Tasks;
using HMS.API.Application.Payments.DTOs;
using HMS.API.Application.Common;

namespace HMS.API.Application.Payments
{
    public interface IPaymentService
    {
        Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest request);
        Task<PaymentDto?> GetPaymentAsync(Guid id);
        Task<ReceiptDto> GenerateReceiptAsync(Guid paymentId);

        Task<PagedResult<PaymentDto>> ListPaymentsAsync(Guid? invoiceId = null, Guid? patientId = null, int page = 1, int pageSize = 20);

        Task<RefundDto> CreateRefundAsync(Guid paymentId, RefundRequest request);

        Task<PagedResult<RefundDto>> ListRefundsAsync(Guid? paymentId = null, Guid? patientId = null, int page = 1, int pageSize = 20);

        Task<RefundReversalDto> CreateRefundReversalAsync(Guid refundId, RefundReversalRequest request);
    }
}