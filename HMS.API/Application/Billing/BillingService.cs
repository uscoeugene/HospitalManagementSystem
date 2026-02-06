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
using HMS.API.Domain.Payments;

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
                    Currency = request.Items.FirstOrDefault()?.SourceType == "" ? "USD" : "USD" // placeholder
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

            var debts = await GetDebtsForInvoiceAsync(inv.Id);

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
                Payments = inv.Payments.Select(p => new InvoicePaymentDto { Id = p.Id, InvoiceId = p.InvoiceId, Amount = p.Amount, PaidAt = p.PaidAt, ExternalReference = p.ExternalReference }).ToArray(),
                Debts = debts.ToArray()
            };
        }

        public async Task<IEnumerable<DTOs.DebtDto>> GetDebtsForInvoiceAsync(Guid invoiceId)
        {
            var q = _db.DebtorEntries.AsNoTracking().Where(d => d.InvoiceId == invoiceId && !d.IsDeleted);
            var items = await q.ToListAsync();
            return items.Select(d => new DTOs.DebtDto { Id = d.Id, InvoiceId = d.InvoiceId, SourceItemId = d.SourceItemId, SourceType = d.SourceType, AmountOwed = d.AmountOwed, Reason = d.Reason, CreatedAt = d.CreatedAt, CreatedBy = d.CreatedBy }).ToList();
        }

        // Reconcile payments against debts before applying to invoice balance
        public async Task<InvoiceDto> ApplyPaymentAsync(Guid invoiceId, ApplyPaymentRequest request)
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var inv = await _db.Invoices.Include(i => i.Items).Include(i => i.Payments).SingleOrDefaultAsync(i => i.Id == invoiceId);
                if (inv == null) throw new InvalidOperationException("Invoice not found");

                if (request.Amount <= 0) throw new InvalidOperationException("Payment amount must be positive");

                var remaining = request.Amount;

                // First, reconcile with debtor entries for this invoice
                var debts = await _db.DebtorEntries.Where(d => d.InvoiceId == invoiceId && !d.IsResolved).OrderBy(d => d.CreatedAt).ToListAsync();
                foreach (var debt in debts)
                {
                    if (remaining <= 0) break;
                    var apply = Math.Min(debt.AmountOwed, remaining);
                    debt.AmountOwed -= apply;
                    remaining -= apply;

                    if (debt.AmountOwed <= 0)
                    {
                        debt.IsResolved = true;
                        debt.ResolvedAt = DateTimeOffset.UtcNow;
                        debt.ResolvedBy = _currentUserService.UserId ?? Guid.Empty;
                    }

                    _db.BillingAudits.Add(new BillingAudit { UserId = _currentUserService.UserId ?? Guid.Empty, Action = "DebtPaymentApplied", Details = $"Applied {apply} to debt {debt.Id} for invoice {invoiceId}" });
                }

                // If any amount remains, create an InvoicePayment entry and apply to invoice
                if (remaining > 0)
                {
                    var invPayment = new InvoicePayment
                    {
                        InvoiceId = inv.Id,
                        Amount = remaining,
                        PaidAt = DateTimeOffset.UtcNow,
                        ExternalReference = request.ExternalReference
                    };
                    _db.InvoicePayments.Add(invPayment);

                    inv.AmountPaid += remaining;

                    _db.BillingAudits.Add(new BillingAudit { UserId = _currentUserService.UserId ?? Guid.Empty, Action = "ApplyPayment", Details = $"Payment of {remaining} applied to invoice {inv.InvoiceNumber}" });
                }

                // Update invoice status
                var prevStatus = inv.Status;
                if (inv.AmountPaid >= inv.TotalAmount)
                {
                    inv.Status = InvoiceStatus.PAID;
                }
                else if (inv.AmountPaid > 0)
                {
                    inv.Status = InvoiceStatus.PARTIAL;
                }

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
                Payments = inv.Payments.Select(p => new InvoicePaymentDto { Id = p.Id, InvoiceId = p.InvoiceId, Amount = p.Amount, PaidAt = p.PaidAt, ExternalReference = p.ExternalReference }).ToArray(),
                Debts = new DebtDto[] { }
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

        public async Task<InvoiceDto> CreateInvoiceFromLabRequestAsync(CreateInvoiceFromLabRequest request)
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
                    Currency = request.Currency
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
                _db.BillingAudits.Add(new BillingAudit { UserId = userId, Action = "CreateInvoiceFromLab", Details = $"Invoice {invoice.InvoiceNumber} created from lab request" });
                await _db.SaveChangesAsync();

                await tx.CommitAsync();

                // write outbox event for invoice created/charged
                var outbox = new OutboxMessage
                {
                    Type = "LabInvoiceCreated",
                    Content = JsonSerializer.Serialize(new { InvoiceId = invoice.Id, PatientId = invoice.PatientId, VisitId = invoice.VisitId }),
                    OccurredAt = DateTimeOffset.UtcNow
                };
                _db.OutboxMessages.Add(outbox);
                await _db.SaveChangesAsync();

                return await GetInvoiceAsync(invoice.Id) ?? throw new InvalidOperationException("Failed to load created invoice");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<IEnumerable<DTOs.DebtDto>> ListDebtsAsync(Guid? patientId = null, bool unresolvedOnly = true)
        {
            var q = _db.DebtorEntries.AsNoTracking().Where(d => !d.IsDeleted);
            if (unresolvedOnly) q = q.Where(d => !d.IsResolved);
            if (patientId.HasValue)
            {
                q = q.Where(d => _db.Invoices.Any(i => i.Id == d.InvoiceId && i.PatientId == patientId.Value));
            }

            var items = await q.ToListAsync();
            return items.Select(d => new DTOs.DebtDto { Id = d.Id, InvoiceId = d.InvoiceId, SourceItemId = d.SourceItemId, SourceType = d.SourceType, AmountOwed = d.AmountOwed, Reason = d.Reason, CreatedAt = d.CreatedAt, CreatedBy = d.CreatedBy }).ToList();
        }

        public async Task<PagedResult<DTOs.DebtDto>> ListDebtsPagedAsync(Guid? invoiceId = null, Guid? patientId = null, bool unresolvedOnly = true, int page = 1, int pageSize = 20)
        {
            var q = _db.DebtorEntries.AsNoTracking().Where(d => !d.IsDeleted);
            if (unresolvedOnly) q = q.Where(d => !d.IsResolved);
            if (invoiceId.HasValue) q = q.Where(d => d.InvoiceId == invoiceId.Value);
            if (patientId.HasValue) q = q.Where(d => _db.Invoices.Any(i => i.Id == d.InvoiceId && i.PatientId == patientId.Value));

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(d => d.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var dtos = items.Select(d => new DTOs.DebtDto { Id = d.Id, InvoiceId = d.InvoiceId, SourceItemId = d.SourceItemId, SourceType = d.SourceType, AmountOwed = d.AmountOwed, Reason = d.Reason, CreatedAt = d.CreatedAt, CreatedBy = d.CreatedBy }).ToArray();

            return new PagedResult<DTOs.DebtDto> { Items = dtos, TotalCount = total, Page = page, PageSize = pageSize };
        }

        public async Task ResolveDebtAsync(Guid debtId)
        {
            var d = await _db.DebtorEntries.SingleOrDefaultAsync(x => x.Id == debtId);
            if (d == null) throw new InvalidOperationException("Debt not found");
            d.IsResolved = true;
            d.ResolvedAt = DateTimeOffset.UtcNow;
            d.ResolvedBy = _currentUserService.UserId ?? Guid.Empty;
            _db.BillingAudits.Add(new BillingAudit { UserId = d.ResolvedBy ?? Guid.Empty, Action = "ResolveDebt", Details = $"Debt {debtId} marked resolved" });
            await _db.SaveChangesAsync();
        }

        public async Task PayDebtAsync(Guid debtId, decimal amount, string? externalReference = null)
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var debt = await _db.DebtorEntries.SingleOrDefaultAsync(d => d.Id == debtId && !d.IsDeleted);
                if (debt == null) throw new InvalidOperationException("Debt not found");
                if (debt.IsResolved) throw new InvalidOperationException("Debt already resolved");

                if (amount <= 0) throw new InvalidOperationException("Amount must be positive");

                var toApply = Math.Min(debt.AmountOwed, amount);

                // create a payment record (associate with invoice and patient inferred)
                var invoice = await _db.Invoices.SingleOrDefaultAsync(i => i.Id == debt.InvoiceId);
                if (invoice == null) throw new InvalidOperationException("Linked invoice not found");

                var payment = new Domain.Payments.Payment
                {
                    InvoiceId = invoice.Id,
                    PatientId = invoice.PatientId,
                    Amount = toApply,
                    Currency = invoice.Currency,
                    ExternalReference = externalReference,
                    Status = Domain.Payments.PaymentStatus.CONFIRMED,
                    CreatedByUserId = _currentUserService.UserId ?? Guid.Empty
                };
                _db.Payments.Add(payment);

                // apply to invoice as invoice payment
                var invPayment = new InvoicePayment { InvoiceId = invoice.Id, Amount = toApply, PaidAt = DateTimeOffset.UtcNow, ExternalReference = externalReference };
                _db.InvoicePayments.Add(invPayment);
                invoice.AmountPaid += toApply;

                // reduce debt
                debt.AmountOwed -= toApply;
                if (debt.AmountOwed <= 0)
                {
                    debt.IsResolved = true;
                    debt.ResolvedAt = DateTimeOffset.UtcNow;
                    debt.ResolvedBy = _currentUserService.UserId ?? Guid.Empty;
                }

                // create receipt
                var receipt = new Domain.Payments.Receipt { Payment = payment, ReceiptNumber = $"RCPT-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N").Substring(0,6).ToUpperInvariant()}", Details = $"Payment of {toApply} applied to debt {debtId}" };
                _db.Receipts.Add(receipt);
                payment.Receipt = receipt;

                _db.BillingAudits.Add(new BillingAudit { UserId = _currentUserService.UserId ?? Guid.Empty, Action = "PayDebt", Details = $"Applied {toApply} to debt {debtId}" });

                // update invoice status
                if (invoice.AmountPaid >= invoice.TotalAmount) invoice.Status = InvoiceStatus.PAID;
                else if (invoice.AmountPaid > 0) invoice.Status = InvoiceStatus.PARTIAL;

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                // publish outbox
                try
                {
                    _db.OutboxMessages.Add(new OutboxMessage { Type = "DebtPaymentCreated", Content = JsonSerializer.Serialize(new { DebtId = debt.Id, PaymentAmount = toApply, InvoiceId = invoice.Id }), OccurredAt = DateTimeOffset.UtcNow });
                    await _db.SaveChangesAsync();
                }
                catch { }
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        // Add methods to BillingService
        public async Task<IEnumerable<DTOs.DebtAgingDto>> GetDebtAgingReportAsync(int daysBucket = 30)
        {
            var now = DateTimeOffset.UtcNow;
            var debts = await _db.DebtorEntries.AsNoTracking().Where(d => !d.IsResolved && !d.IsDeleted).ToListAsync();
            var buckets = new List<DTOs.DebtAgingDto>();
            // create buckets 0-daysBucket, daysBucket+1 - 2*daysBucket, etc up to 365 days
            for (int i = 0; i < 12; i++)
            {
                var from = i * daysBucket;
                var to = (i + 1) * daysBucket - 1;
                var total = debts.Where(d => (now - d.CreatedAt).TotalDays >= from && (now - d.CreatedAt).TotalDays <= to).Sum(d => d.AmountOwed);
                buckets.Add(new DTOs.DebtAgingDto { DaysFrom = from, DaysTo = to, TotalOwed = total });
            }
            // catch-all bucket for > 12*daysBucket
            var more = debts.Where(d => (now - d.CreatedAt).TotalDays > 12 * daysBucket).Sum(d => d.AmountOwed);
            buckets.Add(new DTOs.DebtAgingDto { DaysFrom = 12 * daysBucket, DaysTo = int.MaxValue, TotalOwed = more });
            return buckets;
        }

        public async Task<IEnumerable<DTOs.OutstandingByPatientDto>> GetOutstandingByPatientAsync()
        {
            var q = _db.DebtorEntries.AsNoTracking().Where(d => !d.IsResolved && !d.IsDeleted);
            var byPatient = q.GroupBy(d => _db.Invoices.Where(i => i.Id == d.InvoiceId).Select(i => i.PatientId).FirstOrDefault())
                .Select(g => new DTOs.OutstandingByPatientDto { PatientId = g.Key, TotalOwed = g.Sum(x => x.AmountOwed), DebtCount = g.Count() });

            return await byPatient.ToListAsync();
        }
        public async Task<IEnumerable<DTOs.DebtPaymentResultDto>> PayMultipleDebtsAsync(IEnumerable<DTOs.BatchPayDebtRequest> requests)
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var results = new List<DTOs.DebtPaymentResultDto>();
                foreach (var req in requests)
                {
                    var debt = await _db.DebtorEntries.SingleOrDefaultAsync(d => d.Id == req.DebtId && !d.IsDeleted);
                    if (debt == null)
                    {
                        results.Add(new DTOs.DebtPaymentResultDto { DebtId = req.DebtId, Success = false, Message = "Debt not found" });
                        continue;
                    }
                    if (debt.IsResolved)
                    {
                        results.Add(new DTOs.DebtPaymentResultDto { DebtId = req.DebtId, Success = false, Message = "Debt already resolved" });
                        continue;
                    }

                    var toApply = Math.Min(debt.AmountOwed, req.Amount);
                    if (toApply <= 0)
                    {
                        results.Add(new DTOs.DebtPaymentResultDto { DebtId = req.DebtId, Success = false, Message = "Invalid amount" });
                        continue;
                    }

                    var invoice = await _db.Invoices.SingleOrDefaultAsync(i => i.Id == debt.InvoiceId);
                    if (invoice == null)
                    {
                        results.Add(new DTOs.DebtPaymentResultDto { DebtId = req.DebtId, Success = false, Message = "Linked invoice not found" });
                        continue;
                    }

                    var payment = new Domain.Payments.Payment
                    {
                        InvoiceId = invoice.Id,
                        PatientId = invoice.PatientId,
                        Amount = toApply,
                        Currency = invoice.Currency,
                        ExternalReference = req.ExternalReference,
                        Status = Domain.Payments.PaymentStatus.CONFIRMED,
                        CreatedByUserId = _currentUserService.UserId ?? Guid.Empty
                    };
                    _db.Payments.Add(payment);

                    var invPayment = new InvoicePayment { InvoiceId = invoice.Id, Amount = toApply, PaidAt = DateTimeOffset.UtcNow, ExternalReference = req.ExternalReference };
                    _db.InvoicePayments.Add(invPayment);
                    invoice.AmountPaid += toApply;

                    debt.AmountOwed -= toApply;
                    if (debt.AmountOwed <= 0)
                    {
                        debt.IsResolved = true;
                        debt.ResolvedAt = DateTimeOffset.UtcNow;
                        debt.ResolvedBy = _currentUserService.UserId ?? Guid.Empty;
                    }

                    var receipt = new Domain.Payments.Receipt { Payment = payment, ReceiptNumber = $"RCPT-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N").Substring(0,6).ToUpperInvariant()}", Details = $"Payment of {toApply} applied to debt {debt.Id}" };
                    _db.Receipts.Add(receipt);
                    payment.Receipt = receipt;

                    _db.BillingAudits.Add(new BillingAudit { UserId = _currentUserService.UserId ?? Guid.Empty, Action = "PayDebtBatch", Details = $"Applied {toApply} to debt {debt.Id}" });

                    if (invoice.AmountPaid >= invoice.TotalAmount) invoice.Status = InvoiceStatus.PAID;
                    else if (invoice.AmountPaid > 0) invoice.Status = InvoiceStatus.PARTIAL;

                    results.Add(new DTOs.DebtPaymentResultDto { DebtId = debt.Id, Success = true, Message = "Paid", AppliedAmount = toApply });
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                try
                {
                    _db.OutboxMessages.Add(new OutboxMessage { Type = "DebtBatchPaymentCreated", Content = JsonSerializer.Serialize(new { Count = requests.Count() }), OccurredAt = DateTimeOffset.UtcNow });
                    await _db.SaveChangesAsync();
                }
                catch { }

                return results;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
    }
}