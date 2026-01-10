using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Application.Common;
using HMS.API.Application.Payments.DTOs;
using HMS.API.Domain.Payments;
using HMS.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using HMS.API.Domain.Common;

namespace HMS.API.Application.Payments
{
    public class PaymentService : IPaymentService
    {
        private readonly HmsDbContext _db;
        private readonly ICurrentUserService _currentUserService;

        public PaymentService(HmsDbContext db, ICurrentUserService currentUserService)
        {
            _db = db;
            _currentUserService = currentUserService;
        }

        private string GenerateReceiptNumber() => $"RCPT-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N").Substring(0,6).ToUpperInvariant()}";

        public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest request)
        {
            // only cashier roles should be allowed; controller attribute will enforce, here we still perform validation

            // validate invoice exists
            var invoice = await _db.Invoices.SingleOrDefaultAsync(i => i.Id == request.InvoiceId);
            if (invoice == null) throw new InvalidOperationException("Invoice not found");

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var payment = new Payment
                {
                    InvoiceId = request.InvoiceId,
                    PatientId = request.PatientId,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    ExternalReference = request.ExternalReference,
                    Status = PaymentStatus.CONFIRMED,
                    CreatedByUserId = _currentUserService.UserId ?? Guid.Empty
                };

                _db.Payments.Add(payment);

                // update invoice: amountPaid and status
                var prevStatus = invoice.Status;
                invoice.AmountPaid += payment.Amount;
                if (invoice.AmountPaid >= invoice.TotalAmount) invoice.Status = Domain.Billing.InvoiceStatus.PAID;
                else if (invoice.AmountPaid > 0) invoice.Status = Domain.Billing.InvoiceStatus.PARTIAL;

                // generate receipt
                var receipt = new Receipt
                {
                    Payment = payment,
                    ReceiptNumber = GenerateReceiptNumber(),
                    Details = $"Payment of {payment.Amount} {payment.Currency} for invoice {invoice.InvoiceNumber}"
                };
                _db.Receipts.Add(receipt);
                payment.Receipt = receipt;

                // audit
                _db.BillingAudits.Add(new Domain.Billing.BillingAudit { UserId = payment.CreatedByUserId, Action = "CreatePayment", Details = $"Payment {payment.Id} created for invoice {invoice.InvoiceNumber}" });

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                // publish outbox events for payment and invoice status change
                try
                {
                    var paymentEvent = new { PaymentId = payment.Id, InvoiceId = payment.InvoiceId, Amount = payment.Amount, PatientId = payment.PatientId };
                    _db.OutboxMessages.Add(new OutboxMessage { Type = "PaymentCreated", Content = JsonSerializer.Serialize(paymentEvent), OccurredAt = DateTimeOffset.UtcNow });

                    if (prevStatus.ToString() != invoice.Status.ToString())
                    {
                        var invEvent = new HMS.API.Application.Billing.InvoiceStatusChangedEvent { InvoiceId = invoice.Id, NewStatus = invoice.Status.ToString() };
                        _db.OutboxMessages.Add(new OutboxMessage { Type = nameof(HMS.API.Application.Billing.InvoiceStatusChangedEvent), Content = JsonSerializer.Serialize(invEvent), OccurredAt = DateTimeOffset.UtcNow });
                    }

                    await _db.SaveChangesAsync();
                }
                catch
                {
                    // do not fail the payment if outbox write fails; it will be retried via other mechanisms
                }

                return new PaymentDto
                {
                    Id = payment.Id,
                    InvoiceId = payment.InvoiceId,
                    PatientId = payment.PatientId,
                    Amount = payment.Amount,
                    Currency = payment.Currency,
                    Status = payment.Status.ToString(),
                    ExternalReference = payment.ExternalReference
                };
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<PaymentDto?> GetPaymentAsync(Guid id)
        {
            var p = await _db.Payments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (p == null) return null;
            return new PaymentDto
            {
                Id = p.Id,
                InvoiceId = p.InvoiceId,
                PatientId = p.PatientId,
                Amount = p.Amount,
                Currency = p.Currency,
                Status = p.Status.ToString(),
                ExternalReference = p.ExternalReference
            };
        }

        public async Task<ReceiptDto> GenerateReceiptAsync(Guid paymentId)
        {
            var payment = await _db.Payments.Include(p => p.Receipt).SingleOrDefaultAsync(p => p.Id == paymentId);
            if (payment == null) throw new InvalidOperationException("Payment not found");
            if (payment.Receipt == null) throw new InvalidOperationException("Receipt not found");

            return new ReceiptDto
            {
                Id = payment.Receipt.Id,
                ReceiptNumber = payment.Receipt.ReceiptNumber,
                PaymentId = payment.Id,
                IssuedAt = payment.Receipt.IssuedAt,
                Details = payment.Receipt.Details
            };
        }

        public async Task<PagedResult<PaymentDto>> ListPaymentsAsync(Guid? invoiceId = null, Guid? patientId = null, int page = 1, int pageSize = 20)
        {
            var q = _db.Payments.AsNoTracking().Where(p => !p.IsDeleted);
            if (invoiceId.HasValue) q = q.Where(p => p.InvoiceId == invoiceId.Value);
            if (patientId.HasValue) q = q.Where(p => p.PatientId == patientId.Value);

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(p => p.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var dtos = items.Select(p => new PaymentDto { Id = p.Id, InvoiceId = p.InvoiceId, PatientId = p.PatientId, Amount = p.Amount, Currency = p.Currency, Status = p.Status.ToString(), ExternalReference = p.ExternalReference }).ToArray();

            return new PagedResult<PaymentDto> { Items = dtos, TotalCount = total, Page = page, PageSize = pageSize };
        }

        public async Task<RefundDto> CreateRefundAsync(Guid paymentId, RefundRequest request)
        {
            var payment = await _db.Payments.Include(p => p.Receipt).SingleOrDefaultAsync(p => p.Id == paymentId);
            if (payment == null) throw new InvalidOperationException("Payment not found");
            if (payment.Status != PaymentStatus.CONFIRMED) throw new InvalidOperationException("Only confirmed payments can be refunded");

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var refund = new Domain.Payments.Refund
                {
                    PaymentId = payment.Id,
                    Amount = request.Amount,
                    RefundedAt = DateTimeOffset.UtcNow,
                    ProcessedBy = _currentUserService.UserId ?? Guid.Empty,
                    Reason = request.Reason
                };

                _db.Refunds.Add(refund);

                // audit
                _db.BillingAudits.Add(new Domain.Billing.BillingAudit { UserId = refund.ProcessedBy, Action = "CreateRefund", Details = $"Refund {refund.Id} of {refund.Amount} for payment {payment.Id}" });

                // publish outbox event
                var outbox = new OutboxMessage { Type = "PaymentRefunded", Content = JsonSerializer.Serialize(new { PaymentId = payment.Id, RefundId = refund.Id, Amount = refund.Amount }), OccurredAt = DateTimeOffset.UtcNow };
                _db.OutboxMessages.Add(outbox);

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return new RefundDto { Id = refund.Id, PaymentId = refund.PaymentId, Amount = refund.Amount, RefundedAt = refund.RefundedAt, ProcessedBy = refund.ProcessedBy, Reason = refund.Reason };
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<PagedResult<RefundDto>> ListRefundsAsync(Guid? paymentId = null, Guid? patientId = null, int page = 1, int pageSize = 20)
        {
            var q = _db.Refunds.AsNoTracking().Where(r => !r.IsDeleted);
            if (paymentId.HasValue) q = q.Where(r => r.PaymentId == paymentId.Value);
            if (patientId.HasValue) q = q.Where(r => r.Payment.PatientId == patientId.Value);

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(r => r.RefundedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var dtos = items.Select(r => new RefundDto { Id = r.Id, PaymentId = r.PaymentId, Amount = r.Amount, RefundedAt = r.RefundedAt, ProcessedBy = r.ProcessedBy, Reason = r.Reason }).ToArray();
            return new PagedResult<RefundDto> { Items = dtos, TotalCount = total, Page = page, PageSize = pageSize };
        }

        public async Task<RefundReversalDto> CreateRefundReversalAsync(Guid refundId, RefundReversalRequest request)
        {
            var refund = await _db.Refunds.Include(r => r.Payment).SingleOrDefaultAsync(r => r.Id == refundId);
            if (refund == null) throw new InvalidOperationException("Refund not found");
            if (refund.IsReversed) throw new InvalidOperationException("Refund already reversed");

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var reversal = new Domain.Payments.RefundReversal
                {
                    RefundId = refund.Id,
                    ProcessedBy = _currentUserService.UserId ?? Guid.Empty,
                    ProcessedAt = DateTimeOffset.UtcNow,
                    Reason = request.Reason
                };

                _db.RefundReversals.Add(reversal);

                // mark refund reversed
                refund.IsReversed = true;
                refund.ReversedAt = reversal.ProcessedAt;
                refund.ReversedBy = reversal.ProcessedBy;
                refund.ReversalId = reversal.Id;

                // audit
                _db.BillingAudits.Add(new Domain.Billing.BillingAudit { UserId = reversal.ProcessedBy, Action = "RefundReversal", Details = $"Refund {refund.Id} reversed by {reversal.ProcessedBy}" });

                var outbox = new OutboxMessage { Type = "RefundReversed", Content = JsonSerializer.Serialize(new { RefundId = refund.Id, ReversalId = reversal.Id }), OccurredAt = DateTimeOffset.UtcNow };
                _db.OutboxMessages.Add(outbox);

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return new RefundReversalDto { Id = reversal.Id, RefundId = reversal.RefundId, ProcessedAt = reversal.ProcessedAt, ProcessedBy = reversal.ProcessedBy, Reason = reversal.Reason };
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
    }
}