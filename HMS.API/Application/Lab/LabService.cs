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
            // ensure invoice is PAID for visit if visit provided
            if (request.VisitId.HasValue)
            {
                var invoiceDto = await _billing.ListInvoicesAsync(null, request.VisitId.Value, null, 1, 1);
                var invoice = invoiceDto.Items.FirstOrDefault();
                if (invoice != null && invoice.Status != "PAID" && !request.AllowOnCredit) throw new InvalidOperationException("Invoice for visit must be paid before processing lab request");
            }

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var lr = new LabRequest { PatientId = request.PatientId, VisitId = request.VisitId, Status = LabRequestStatus.ORDERED };
                var billingItems = new List<CreateInvoiceItemRequest>();

                foreach (var it in request.Items)
                {
                    var test = await _db.LabTests.FindAsync(it.LabTestId);
                    if (test == null) throw new InvalidOperationException("Lab test not found");
                    var item = new LabRequestItem { LabTest = test, Price = test.Price, Currency = test.Currency };
                    lr.Items.Add(item);

                    billingItems.Add(new CreateInvoiceItemRequest { Description = test.Name, UnitPrice = test.Price, Quantity = 1, SourceId = item.Id, SourceType = "lab" });
                }

                _db.LabRequests.Add(lr);
                await _db.SaveChangesAsync();

                // delegate invoice creation to BillingService
                var invoiceReq = new CreateInvoiceFromLabRequest { PatientId = lr.PatientId, VisitId = lr.VisitId, Items = billingItems.ToArray(), Currency = "USD" };
                var invoice = await _billing.CreateInvoiceFromLabRequestAsync(invoiceReq);

                // update lab request items with charge references
                foreach (var li in lr.Items)
                {
                    var matched = invoice.Items.FirstOrDefault(i => i.SourceId == li.Id && i.SourceType == "lab");
                    if (matched != null)
                    {
                        li.ChargeInvoiceItemId = matched.Id;

                        // If invoice is unpaid and AllowOnCredit is true, create DebtorEntry
                        var inv = await _db.Invoices.Include(i => i.Items).Include(i => i.Payments).Where(i => i.Items.Any(ii => ii.Id == matched.Id)).OrderByDescending(i => i.CreatedAt).FirstOrDefaultAsync();
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

                lr.Status = LabRequestStatus.CHARGED;
                await _db.SaveChangesAsync();

                await tx.CommitAsync();

                return new LabRequestDto { Id = lr.Id, PatientId = lr.PatientId, VisitId = lr.VisitId, Status = lr.Status.ToString(), Tests = lr.Items.Select(i => new LabTestDto { Id = i.LabTestId, Code = i.LabTest.Code, Name = i.LabTest.Name, Description = i.LabTest.Description, Price = i.Price, Currency = i.Currency }).ToArray() };
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
            return new LabRequestDto { Id = lr.Id, PatientId = lr.PatientId, VisitId = lr.VisitId, Status = lr.Status.ToString(), Tests = lr.Items.Select(i => new LabTestDto { Id = i.LabTestId, Code = i.LabTest.Code, Name = i.LabTest.Name, Description = i.LabTest.Description, Price = i.Price, Currency = i.Currency }).ToArray() };
        }

        public async Task<PagedResult<LabRequestDto>> ListRequestsAsync(Guid? patientId = null, string? status = null, int page = 1, int pageSize = 20)
        {
            var q = _db.LabRequests.AsNoTracking().Include(r => r.Items).ThenInclude(i => i.LabTest).Where(r => !r.IsDeleted);
            if (patientId.HasValue) q = q.Where(r => r.PatientId == patientId.Value);
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse(typeof(LabRequestStatus), status, true, out var st)) q = q.Where(r => r.Status == (LabRequestStatus)st);

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(r => r.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var dtos = items.Select(lr => new LabRequestDto { Id = lr.Id, PatientId = lr.PatientId, VisitId = lr.VisitId, Status = lr.Status.ToString(), Tests = lr.Items.Select(i => new LabTestDto { Id = i.LabTestId, Code = i.LabTest.Code, Name = i.LabTest.Name, Description = i.LabTest.Description, Price = i.Price, Currency = i.Currency }).ToArray() }).ToArray();

            return new PagedResult<LabRequestDto> { Items = dtos, TotalCount = total, Page = page, PageSize = pageSize };
        }
    }
}