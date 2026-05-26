using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Application.Lab.DTOs;
using HMS.API.Application.Billing;
using HMS.API.Application.Billing.DTOs;
using HMS.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HMS.API.Application.Common;
using HMS.API.Domain.Lab;
using System.Collections.Generic;
using HMS.API.Domain.Billing;
using System.Text.Json;

namespace HMS.API.Application.Lab
{
    public class LabService : ILabService
    {
        private readonly HmsDbContext _db;
        private readonly IBillingService _billing;
        private readonly ICurrentUserService _currentUserService;

        public LabService(HmsDbContext db, IBillingService billing, ICurrentUserService currentUserService)
        {
            _db = db;
            _billing = billing;
            _currentUserService = currentUserService;
        }

        public async Task<LabTestDto[]> ListTestsAsync()
        {
            return await _db.LabTests.AsNoTracking().Select(t => new LabTestDto { Id = t.Id, Code = t.Code, Name = t.Name, Description = t.Description, Price = t.Price, Currency = t.Currency }).ToArrayAsync();
        }

        private static LabTestDto MapTest(LabTest test) => new()
        {
            Id = test.Id,
            Code = test.Code,
            Name = test.Name,
            Description = test.Description,
            Price = test.Price,
            Currency = test.Currency
        };

        private static LabRequestItemDto MapItem(LabRequestItem item) => new()
        {
            Id = item.Id,
            LabTestId = item.LabTestId,
            LabTest = item.LabTest == null ? new LabTestDto { Id = item.LabTestId } : MapTest(item.LabTest),
            Price = item.Price,
            Currency = item.Currency,
            ChargeInvoiceItemId = item.ChargeInvoiceItemId,
            ResultStatus = item.ResultStatus.ToString(),
            ResultValue = item.ResultValue,
            ResultUnit = item.ResultUnit,
            ReferenceRange = item.ReferenceRange,
            AbnormalFlag = item.AbnormalFlag,
            ResultNotes = item.ResultNotes,
            ResultAttachmentUrl = item.ResultAttachmentUrl,
            ResultedAt = item.ResultedAt,
            ResultedByUserId = item.ResultedByUserId,
            VerifiedAt = item.VerifiedAt,
            VerifiedByUserId = item.VerifiedByUserId
        };

        private static LabRequestItemDto[] MapItemsArray(LabRequest request)
            => request.Items.Select(MapItem).ToArray();

        private async Task<LabRequestDto> MapRequestAsync(LabRequest request)
        {
            var items = MapItemsArray(request);

            var dto = new LabRequestDto
            {
                Id = request.Id,
                RequestNumber = request.RequestNumber,
                PatientId = request.PatientId,
                VisitId = request.VisitId,
                InvoiceId = request.InvoiceId,
                Status = request.Status.ToString(),
                Items = items,
                Tests = items.Select(i => i.LabTest).ToArray(),
                CreatedAt = request.CreatedAt,
                ItemsCount = items.Length,
            };

            // results status
            dto.ResultsStatus = items.Any(i => !string.Equals(i.ResultStatus, "PENDING", StringComparison.OrdinalIgnoreCase)) ? "HasResults" : "Pending";

            // try resolve patient name
            try
            {
                var p = await _db.Patients.AsNoTracking().Where(x => x.Id == request.PatientId).Select(x => new { x.FirstName, x.LastName }).SingleOrDefaultAsync();
                if (p != null) dto.PatientName = (p.FirstName + " " + p.LastName).Trim();
            }
            catch { }

            // try resolve linked invoice summary by matching invoice items source id
            try
            {
                var itemIds = request.Items.Select(i => i.Id).ToArray();
                var invoice = await _db.Invoices.AsNoTracking().Include(i => i.Items)
                                .Where(i => i.Items.Any(ii => ii.SourceId.HasValue && itemIds.Contains(ii.SourceId.Value) && ii.SourceType == "lab"))
                                .OrderByDescending(i => i.CreatedAt)
                                .FirstOrDefaultAsync();

                if (invoice != null)
                {
                    dto.InvoiceSummary = new InvoiceSummaryDto
                    {
                        Id = invoice.Id,
                        InvoiceNumber = invoice.InvoiceNumber,
                        Status = invoice.Status.ToString(),
                        TotalAmount = invoice.TotalAmount,
                        AmountPaid = invoice.AmountPaid,
                        Currency = invoice.Currency
                    };
                }
            }
            catch { }

            return dto;
        }

        public async Task<LabTestDto> CreateTestAsync(LabTestDto dto)
        {
            var t = new LabTest { Code = dto.Code, Name = dto.Name, Description = dto.Description, Price = dto.Price, Currency = dto.Currency };
            _db.LabTests.Add(t);
            await _db.SaveChangesAsync();
            dto.Id = t.Id;
            return dto;
        }

        public async Task<LabRequestDto> CreateRequestAsync(CreateLabRequest request)
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var lr = new LabRequest { PatientId = request.PatientId, VisitId = request.VisitId, Status = LabRequestStatus.ORDERED };

                foreach (var it in request.Items)
                {
                    var test = await _db.LabTests.FindAsync(it.LabTestId);
                    if (test == null) throw new InvalidOperationException("Lab test not found");
                    var item = new LabRequestItem { LabTest = test, Price = test.Price, Currency = test.Currency };
                    lr.Items.Add(item);
                }

                _db.LabRequests.Add(lr);
                // generate a human-friendly request number, include patient id short + timestamp
                lr.RequestNumber = $"LR-{lr.PatientId.ToString().Substring(0,8).ToUpperInvariant()}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
                await _db.SaveChangesAsync();

                // Now that LabRequestItems have DB-assigned Ids, build billing items using the real source ids
                var billingItems = lr.Items.Select(li => new CreateInvoiceItemRequest { Description = li.LabTest.Name, UnitPrice = li.Price, Quantity = 1, SourceId = li.Id, SourceType = "lab" }).ToList();

                // delegate invoice creation to BillingService and persist linked invoice id
                var invoiceReq = new CreateInvoiceFromLabRequest
                {
                    PatientId = lr.PatientId,
                    VisitId = lr.VisitId,
                    Items = billingItems.ToArray(),
                    Currency = "USD",
                    AllowOnCredit = request.AllowOnCredit,
                    CreditReason = request.CreditReason
                };
                var invoice = await _billing.CreateInvoiceFromLabRequestAsync(invoiceReq);
                if (invoice != null)
                {
                    lr.InvoiceId = invoice.Id; // persist link for faster lookups
                }

                // update lab request items with charge references by querying InvoiceItems directly
                foreach (var li in lr.Items)
                {
                    // try exact match by SourceId
                    var invItem = await _db.Set<Domain.Billing.InvoiceItem>().SingleOrDefaultAsync(ii => ii.SourceId == li.Id && ii.SourceType == "lab");

                    // fallback: if not found, try to find an invoice item on the newly created invoice that matches on description/price and has empty SourceId
                    if (invItem == null && invoice != null)
                    {
                        var possible = await _db.Set<Domain.Billing.InvoiceItem>().Where(ii => ii.InvoiceId == invoice.Id && (ii.SourceId == null || ii.SourceId == Guid.Empty) && ii.Description == li.LabTest.Name && ii.UnitPrice == li.Price && ii.Quantity == 1).ToListAsync();
                        invItem = possible.FirstOrDefault();

                        if (invItem != null)
                        {
                            // assign SourceId/SourceType for future reliable linking
                            invItem.SourceId = li.Id;
                            invItem.SourceType = "lab";
                            _db.Set<Domain.Billing.InvoiceItem>().Update(invItem);
                        }
                    }

                    if (invItem != null)
                    {
                        li.ChargeInvoiceItemId = invItem.Id;

                        // If invoice is unpaid and AllowOnCredit is true, create DebtorEntry
                        var inv = await _db.Invoices.Include(i => i.Items).Include(i => i.Payments).SingleOrDefaultAsync(i => i.Id == invItem.InvoiceId);
                        if (inv != null && inv.Status != Domain.Billing.InvoiceStatus.PAID && request.AllowOnCredit)
                        {
                            if (!(_currentUserService.HasPermission("lab.charge.credit")))
                            {
                                throw new InvalidOperationException("Insufficient permissions to charge lab on credit");
                            }

                            var userId = _currentUserService.UserId ?? Guid.Empty;
                            _db.BillingAudits.Add(new Domain.Billing.BillingAudit { UserId = userId, Action = "LabChargeOnCredit", Details = $"Lab item {li.Id} charged on credit for Invoice {inv.InvoiceNumber}. Reason: {request.CreditReason}" });

                            var debtor = new DebtorEntry
                            {
                                InvoiceId = inv.Id,
                                SourceItemId = li.Id,
                                SourceType = "lab",
                                AmountOwed = li.Price,
                                Reason = request.CreditReason,
                                CreatedBy = userId
                            };
                            _db.DebtorEntries.Add(debtor);
                        }
                    }
                }

                // Persist any invoice item SourceId assignments and debtor entries before finishing
                await _db.SaveChangesAsync();

                lr.Status = LabRequestStatus.CHARGED;
                await _db.SaveChangesAsync();

                await tx.CommitAsync();

                return await MapRequestAsync(lr);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<LabRequestDto?> GetRequestAsync(Guid id)
        {
            var lr = await _db.LabRequests.Include(r => r.Items).ThenInclude(i => i.LabTest).AsNoTracking().SingleOrDefaultAsync(r => r.Id == id);
            if (lr == null) return null;
            return await MapRequestAsync(lr);
        }

        public async Task<PagedResult<LabRequestDto>> ListRequestsAsync(Guid? patientId = null, Guid? visitId = null, string? status = null, int page = 1, int pageSize = 20)
        {
            var q = _db.LabRequests.AsNoTracking().Include(r => r.Items).ThenInclude(i => i.LabTest).Where(r => !r.IsDeleted);
            if (patientId.HasValue) q = q.Where(r => r.PatientId == patientId.Value);
            if (visitId.HasValue) q = q.Where(r => r.VisitId == visitId.Value);
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse(typeof(LabRequestStatus), status, true, out var st)) q = q.Where(r => r.Status == (LabRequestStatus)st);

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(r => r.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var dtos = new List<LabRequestDto>();
            foreach (var it in items)
            {
                dtos.Add(await MapRequestAsync(it));
            }

            return new PagedResult<LabRequestDto> { Items = dtos, TotalCount = total, Page = page, PageSize = pageSize };
        }

        public async Task<LabRequestDto> UpdateResultAsync(Guid requestId, Guid itemId, UpdateLabResultRequest request)
        {
            var lr = await _db.LabRequests.Include(r => r.Items).ThenInclude(i => i.LabTest).SingleOrDefaultAsync(r => r.Id == requestId && !r.IsDeleted);
            if (lr == null) throw new InvalidOperationException("Lab request not found");

            var item = lr.Items.SingleOrDefault(i => i.Id == itemId && !i.IsDeleted);
            if (item == null) throw new InvalidOperationException("Lab request item not found");

            await EnsureLabItemInvoicePaidAsync(item);

            var userId = _currentUserService.UserId;
            item.ResultValue = request.ResultValue;
            item.ResultUnit = request.ResultUnit;
            item.ReferenceRange = request.ReferenceRange;
            item.AbnormalFlag = request.AbnormalFlag;
            item.ResultNotes = request.ResultNotes;
            item.ResultedAt = DateTimeOffset.UtcNow;
            item.ResultedByUserId = userId;
            item.ResultStatus = request.Verify ? LabResultStatus.VERIFIED : LabResultStatus.RESULTED;

            if (request.Verify)
            {
                item.VerifiedAt = DateTimeOffset.UtcNow;
                item.VerifiedByUserId = userId;
            }

            UpdateRequestStatusFromItems(lr);
            await _db.SaveChangesAsync();
            return await MapRequestAsync(lr);
        }

        public async Task<LabRequestDto> AttachResultFileAsync(Guid requestId, Guid itemId, string attachmentUrl)
        {
            var lr = await _db.LabRequests.Include(r => r.Items).ThenInclude(i => i.LabTest).SingleOrDefaultAsync(r => r.Id == requestId && !r.IsDeleted);
            if (lr == null) throw new InvalidOperationException("Lab request not found");

            var item = lr.Items.SingleOrDefault(i => i.Id == itemId && !i.IsDeleted);
            if (item == null) throw new InvalidOperationException("Lab request item not found");

            await EnsureLabItemInvoicePaidAsync(item);

            item.ResultAttachmentUrl = attachmentUrl;
            item.ResultedAt ??= DateTimeOffset.UtcNow;
            item.ResultedByUserId ??= _currentUserService.UserId;
            if (item.ResultStatus == LabResultStatus.PENDING)
            {
                item.ResultStatus = LabResultStatus.RESULTED;
            }

            UpdateRequestStatusFromItems(lr);
            await _db.SaveChangesAsync();
            return await MapRequestAsync(lr);
        }

        private async Task EnsureLabItemInvoicePaidAsync(LabRequestItem item)
        {
            // If ChargeInvoiceItemId not set, try to find invoice item by SourceId and assign it
            if (!item.ChargeInvoiceItemId.HasValue)
            {
                var invItem = await _db.Set<Domain.Billing.InvoiceItem>().AsNoTracking().SingleOrDefaultAsync(ii => ii.SourceId == item.Id && ii.SourceType == "lab");
                if (invItem != null)
                {
                    // set link on item for future checks
                    item.ChargeInvoiceItemId = invItem.Id;
                    // persist link
                    _db.LabRequestItems.Update(item);
                    await _db.SaveChangesAsync();
                }
            }

            // Find the invoice that contains the linked invoice item (or by SourceId fallback)
            var invoice = await _db.Invoices.Include(i => i.Items).AsNoTracking().SingleOrDefaultAsync(i => i.Items.Any(ii => (item.ChargeInvoiceItemId.HasValue && ii.Id == item.ChargeInvoiceItemId.Value) || (ii.SourceId == item.Id && ii.SourceType == "lab")));
            if (invoice == null)
            {
                // fallback: try to locate lab request's linked invoice via LabRequest.InvoiceId
                var lr = await _db.LabRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == item.LabRequestId);
                if (lr != null && lr.InvoiceId.HasValue)
                {
                    invoice = await _db.Invoices.Include(i => i.Items).AsNoTracking().SingleOrDefaultAsync(i => i.Id == lr.InvoiceId.Value);
                }

                if (invoice == null) throw new InvalidOperationException("Linked lab invoice not found");
            }

            // If invoice already fully paid, allow
            if (invoice.Status == InvoiceStatus.PAID) return;

            // If invoice itself is marked as AllowOnCredit, permit updating results regardless of payments
            if (invoice.AllowOnCredit) return;

            // Otherwise, check if this lab item was charged on credit (DebtorEntry exists)
            var debtorExists = await _db.DebtorEntries.AsNoTracking().AnyAsync(d => d.SourceItemId == item.Id && d.SourceType == "lab" && !d.IsDeleted);
            if (debtorExists) return;

            // No credit arrangement found and invoice is not paid -> disallow
            throw new InvalidOperationException("Lab invoice must be paid before results can be updated (or charge-on-credit must be enabled)");
        }

        private static void UpdateRequestStatusFromItems(LabRequest request)
        {
            if (request.Items.All(i => i.ResultStatus is LabResultStatus.RESULTED or LabResultStatus.VERIFIED or LabResultStatus.AMENDED))
            {
                request.Status = LabRequestStatus.COMPLETED;
            }
            else if (request.Items.Any(i => i.ResultStatus != LabResultStatus.PENDING))
            {
                request.Status = LabRequestStatus.PROCESSING;
            }
        }
    }
}
