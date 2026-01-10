using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Application.Billing.DTOs;
using HMS.API.Domain.Billing;
using HMS.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HMS.API.Application.Common;
using System.Collections.Generic;
using System.Text.Json;
using HMS.API.Domain.Common;

namespace HMS.API.Application.Billing
{
    public class BillingService : IBillingService
    {
        private readonly HmsDbContext _db;
        private readonly ICurrentUserService _currentUserService;
        private readonly IEventPublisher _events;

        public BillingService(HmsDbContext db, ICurrentUserService currentUserService, IEventPublisher events)
        {
            _db = db;
            _currentUserService = currentUserService;
            _events = events;
        }

        private string GenerateInvoiceNumber()
        {
            // simple invoice number: INV-{timestamp}-{shortguid}
            return $"INV-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N").Substring(0,6).ToUpperInvariant()}";
        }

        public async Task<InvoiceDto> CreateInvoiceAsync(CreateInvoiceRequest request)
        {
            // validate patient
            var patientExists = await _db.Patients.AnyAsync(p => p.Id == request.PatientId && !p.IsDeleted);
            if (!patientExists) throw new InvalidOperationException("Patient not found");

            if (request.VisitId.HasValue)
            {
                var visitExists = await _db.Visits.AnyAsync(v => v.Id == request.VisitId.Value && !v.IsDeleted);
                if (!visitExists) throw new InvalidOperationException("Visit not found");
            }

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var invoice = new Invoice
                {
                    PatientId = request.PatientId,
                    VisitId = request.VisitId,
                    InvoiceNumber = GenerateInvoiceNumber(),
                    Status = InvoiceStatus.UNPAID,
                    TotalAmount = 0m,
                    AmountPaid = 0m,
                    Currency = request.Items.FirstOrDefault()?.SourceType == "" ? "USD" : "USD" // placeholder, sourceType might indicate currency in future
                };

                foreach (var item in request.Items)
                {
                    var ii = new InvoiceItem
                    {
                        Description = item.Description,
                        UnitPrice = item.UnitPrice,
                        Quantity = item.Quantity,
                        SourceId = item.SourceId,
                        SourceType = item.SourceType
                    };
                    invoice.Items.Add(ii);
                    invoice.TotalAmount += ii.LineTotal;
                }

                _db.Invoices.Add(invoice);
                await _db.SaveChangesAsync();

                // audit
                var userId = _currentUserService.UserId ?? Guid.Empty;
                _db.BillingAudits.Add(new BillingAudit { UserId = userId, Action = "CreateInvoice", Details = $"Invoice {invoice.InvoiceNumber} created" });
                await _db.SaveChangesAsync();

                await tx.CommitAsync();

                // publish event using outbox
                var outbox = new OutboxMessage
                {
                    Type = nameof(InvoiceStatusChangedEvent),
                    Content = JsonSerializer.Serialize(new InvoiceStatusChangedEvent { InvoiceId = invoice.Id, NewStatus = invoice.Status.ToString() }),
                    OccurredAt = DateTimeOffset.UtcNow
                };
                _db.Set<OutboxMessage>().Add(outbox);
                await _db.SaveChangesAsync();

                return await GetInvoiceAsync(invoice.Id) ?? throw new InvalidOperationException("Failed to load created invoice");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<InvoiceDto?> GetInvoiceAsync(Guid id)
        {
            var inv = await _db.Invoices.Include(i => i.Items).Include(i => i.Payments).SingleOrDefaultAsync(i => i.Id == id);
            if (inv == null) return null;
            return new InvoiceDto
            {
                Id = inv.Id,
                InvoiceNumber = inv.InvoiceNumber,
                PatientId = inv.PatientId,
                VisitId = inv.VisitId,
                Status = inv.Status.ToString(),
                TotalAmount = inv.TotalAmount,
                AmountPaid = inv.AmountPaid,
                Currency = inv.Currency,
                Items = inv.Items.Select(i => new InvoiceItemDto { Id = i.Id, Description = i.Description, UnitPrice = i.UnitPrice, Quantity = i.Quantity, LineTotal = i.LineTotal }).ToArray(),
                Payments = inv.Payments.Select(p => new InvoicePaymentDto { Id = p.Id, InvoiceId = p.InvoiceId, Amount = p.Amount, PaidAt = p.PaidAt, ExternalReference = p.ExternalReference }).ToArray()
            };
        }

        public async Task<InvoiceDto> ApplyPaymentAsync(Guid invoiceId, ApplyPaymentRequest request)
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var inv = await _db.Invoices.Include(i => i.Items).Include(i => i.Payments).SingleOrDefaultAsync(i => i.Id == invoiceId);
                if (inv == null) throw new InvalidOperationException("Invoice not found");

                if (request.Amount <= 0) throw new InvalidOperationException("Payment amount must be positive");

                var payment = new InvoicePayment
                {
                    Invoice = inv,
                    Amount = request.Amount,
                    PaidAt = DateTimeOffset.UtcNow,
                    ExternalReference = request.ExternalReference
                };

                inv.Payments.Add(payment);
                inv.AmountPaid += payment.Amount;

                var prevStatus = inv.Status;

                if (inv.AmountPaid >= inv.TotalAmount)
                {
                    inv.Status = InvoiceStatus.PAID;
                }
                else if (inv.AmountPaid > 0)
                {
                    inv.Status = InvoiceStatus.PARTIAL;
                }

                var userId = _currentUserService.UserId ?? Guid.Empty;
                _db.BillingAudits.Add(new BillingAudit { UserId = userId, Action = "ApplyPayment", Details = $"Payment of {payment.Amount} applied to {inv.InvoiceNumber}" });

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                if (prevStatus.ToString() != inv.Status.ToString())
                {
                    var outboxMsg = new OutboxMessage
                    {
                        Type = nameof(InvoiceStatusChangedEvent),
                        Content = JsonSerializer.Serialize(new InvoiceStatusChangedEvent { InvoiceId = inv.Id, NewStatus = inv.Status.ToString() }),
                        OccurredAt = DateTimeOffset.UtcNow
                    };
                    _db.Set<OutboxMessage>().Add(outboxMsg);
                    await _db.SaveChangesAsync();
                }

                return await GetInvoiceAsync(inv.Id) ?? throw new InvalidOperationException("Failed to load invoice after payment");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<PagedResult<InvoiceDto>> ListInvoicesAsync(Guid? patientId = null, Guid? visitId = null, string? status = null, int page = 1, int pageSize = 20)
        {
            var q = _db.Invoices.AsNoTracking().Include(i => i.Items).Where(i => !i.IsDeleted);
            if (patientId.HasValue) q = q.Where(i => i.PatientId == patientId.Value);
            if (visitId.HasValue) q = q.Where(i => i.VisitId == visitId.Value);
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse(typeof(InvoiceStatus), status, true, out var st)) q = q.Where(i => i.Status == (InvoiceStatus)st);

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(i => i.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var dtos = items.Select(inv => new InvoiceDto
            {
                Id = inv.Id,
                InvoiceNumber = inv.InvoiceNumber,
                PatientId = inv.PatientId,
                VisitId = inv.VisitId,
                Status = inv.Status.ToString(),
                TotalAmount = inv.TotalAmount,
                AmountPaid = inv.AmountPaid,
                Currency = inv.Currency,
                Items = inv.Items.Select(i => new InvoiceItemDto { Id = i.Id, Description = i.Description, UnitPrice = i.UnitPrice, Quantity = i.Quantity, LineTotal = i.LineTotal }).ToArray(),
                Payments = inv.Payments.Select(p => new InvoicePaymentDto { Id = p.Id, InvoiceId = p.InvoiceId, Amount = p.Amount, PaidAt = p.PaidAt, ExternalReference = p.ExternalReference }).ToArray()
            }).ToArray();

            return new PagedResult<InvoiceDto> { Items = dtos, TotalCount = total, Page = page, PageSize = pageSize };
        }

        public async Task<PagedResult<InvoicePaymentDto>> ListPaymentsAsync(Guid? invoiceId = null, Guid? patientId = null, int page = 1, int pageSize = 20)
        {
            var q = _db.InvoicePayments.AsNoTracking().Include(p => p.Invoice).Where(p => !p.IsDeleted);

            if (invoiceId.HasValue) q = q.Where(p => p.InvoiceId == invoiceId.Value);
            if (patientId.HasValue) q = q.Where(p => p.Invoice.PatientId == patientId.Value);

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(p => p.PaidAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var dtos = items.Select(p => new InvoicePaymentDto { Id = p.Id, InvoiceId = p.InvoiceId, Amount = p.Amount, PaidAt = p.PaidAt, ExternalReference = p.ExternalReference }).ToArray();

            return new PagedResult<InvoicePaymentDto> { Items = dtos, TotalCount = total, Page = page, PageSize = pageSize };
        }
    }
}