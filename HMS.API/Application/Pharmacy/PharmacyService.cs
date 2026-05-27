using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Application.Pharmacy.DTOs;
using HMS.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HMS.API.Application.Common;
using System.Collections.Generic;
using HMS.API.Application.Billing;
using HMS.API.Application.Billing.DTOs;
using HMS.API.Domain.Common;
using System.Text.Json;
using HMS.API.Domain.Billing;

namespace HMS.API.Application.Pharmacy
{
    public class PharmacyService : IPharmacyService
    {
        private readonly HmsDbContext _db;
        private readonly IBillingService _billing;
        private readonly ICurrentUserService _currentUserService;

        public PharmacyService(HmsDbContext db, IBillingService billing, ICurrentUserService currentUserService)
        {
            _db = db;
            _billing = billing;
            _currentUserService = currentUserService;
        }

        // Drug management removed - use InventoryService for medication management

        public async Task<PrescriptionDto> CreatePrescriptionAsync(CreatePrescriptionRequest req)
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var p = new Domain.Pharmacy.Prescription { PatientId = req.PatientId, VisitId = req.VisitId, Status = Domain.Pharmacy.PrescriptionStatus.ORDERED };
                var billingItems = new List<CreateInvoiceItemRequest>();

                foreach (var it in req.Items)
                {
                    var inv = await _db.InventoryItems.FindAsync(it.InventoryItemId);
                    if (inv == null) throw new InvalidOperationException("Inventory item not found");
                    var available = inv.Stock - inv.ReservedStock;
                    if (available < it.Quantity) throw new InvalidOperationException("Insufficient available stock to reserve for prescription");

                    var pi = new Domain.Pharmacy.PrescriptionItem { InventoryItem = inv, InventoryItemId = inv.Id, Quantity = it.Quantity, Price = inv.UnitPrice, Currency = inv.Currency };
                    p.Items.Add(pi);

                    inv.ReservedStock += it.Quantity;

                    var reservation = new Domain.Pharmacy.Reservation { InventoryItemId = inv.Id, Quantity = it.Quantity, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30), PrescriptionItemId = pi.Id };
                    _db.Reservations.Add(reservation);

                    billingItems.Add(new CreateInvoiceItemRequest { Description = inv.Name, UnitPrice = inv.UnitPrice, Quantity = it.Quantity, SourceId = pi.Id, SourceType = "pharmacy" });
                }

                _db.Prescriptions.Add(p);
                await _db.SaveChangesAsync();

                // delegate invoice creation to billing
                var invoiceReq = new CreateInvoiceFromLabRequest { PatientId = p.PatientId, VisitId = p.VisitId, Items = billingItems.ToArray(), Currency = "USD" };
                var invoice = await _billing.CreateInvoiceFromLabRequestAsync(invoiceReq);

                // map charge ids
                foreach (var pi in p.Items)
                {
                    var matched = invoice.Items.FirstOrDefault(i => i.SourceId == pi.Id && i.SourceType == "pharmacy");
                    if (matched != null) pi.ChargeInvoiceItemId = matched.Id;
                }

                p.Status = Domain.Pharmacy.PrescriptionStatus.CHARGED;
                await _db.SaveChangesAsync();

                // publish outbox event for prescription charged
                var outboxCharged = new OutboxMessage { Type = "PrescriptionCharged", Content = JsonSerializer.Serialize(new HMS.API.Application.Pharmacy.Events.PrescriptionChargedEvent { PrescriptionId = p.Id, InvoiceId = invoice.Id, PatientId = p.PatientId }), OccurredAt = DateTimeOffset.UtcNow };
                _db.OutboxMessages.Add(outboxCharged);
                await _db.SaveChangesAsync();

                await tx.CommitAsync();

                return new PrescriptionDto { Id = p.Id, PatientId = p.PatientId, VisitId = p.VisitId, Status = p.Status.ToString(), Items = p.Items.Select(i => new PrescriptionItemDto { Id = i.Id, InventoryItemId = i.InventoryItemId, Name = i.InventoryItem!.Name, Quantity = i.Quantity, DispensedQuantity = i.DispensedQuantity, Price = i.Price, Currency = i.Currency }).ToArray() };
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<PrescriptionDto?> GetPrescriptionAsync(Guid id)
        {
            var p = await _db.Prescriptions.Include(pr => pr.Items).ThenInclude(i => i.InventoryItem).AsNoTracking().SingleOrDefaultAsync(pr => pr.Id == id);
            if (p == null) return null;
            return new PrescriptionDto { Id = p.Id, PatientId = p.PatientId, VisitId = p.VisitId, Status = p.Status.ToString(), Items = p.Items.Select(i => new PrescriptionItemDto { Id = i.Id, InventoryItemId = i.InventoryItemId, Name = i.InventoryItem!.Name, Quantity = i.Quantity, DispensedQuantity = i.DispensedQuantity, Price = i.Price, Currency = i.Currency }).ToArray() };
        }

        public async Task<PagedResult<PrescriptionDto>> ListPrescriptionsAsync(Guid? patientId = null, string? status = null, int page = 1, int pageSize = 20)
        {
            var q = _db.Prescriptions.AsNoTracking().Include(pr => pr.Items).ThenInclude(i => i.InventoryItem).Where(pr => !pr.IsDeleted);
            if (patientId.HasValue) q = q.Where(pr => pr.PatientId == patientId.Value);
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse(typeof(Domain.Pharmacy.PrescriptionStatus), status, true, out var st)) q = q.Where(pr => pr.Status == (Domain.Pharmacy.PrescriptionStatus)st);

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(pr => pr.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var dtos = items.Select(pr => new PrescriptionDto { Id = pr.Id, PatientId = pr.PatientId, VisitId = pr.VisitId, Status = pr.Status.ToString(), Items = pr.Items.Select(i => new PrescriptionItemDto { Id = i.Id, InventoryItemId = i.InventoryItemId, Name = i.InventoryItem!.Name, Quantity = i.Quantity, DispensedQuantity = i.DispensedQuantity, Price = i.Price, Currency = i.Currency }).ToArray() }).ToArray();

            return new PagedResult<PrescriptionDto> { Items = dtos, TotalCount = total, Page = page, PageSize = pageSize };
        }

        public async Task<DispenseDto> DispenseAsync(DispenseRequest req)
        {
            // Ensure prescription item exists
            var item = await _db.PrescriptionItems.Include(pi => pi.Prescription).Include(pi => pi.InventoryItem).SingleOrDefaultAsync(pi => pi.Id == req.PrescriptionItemId);
            if (item == null) throw new InvalidOperationException("Prescription item not found");

            // ensure invoice is PAID for the linked charge if exists
            bool isOnCredit = false;
            if (item.ChargeInvoiceItemId.HasValue)
            {
                var invoiceItemId = item.ChargeInvoiceItemId.Value;
                var invoice = await _db.Invoices.Include(i => i.Items).Include(i => i.Payments).Where(i => i.Items.Any(ii => ii.Id == invoiceItemId)).OrderByDescending(i => i.CreatedAt).FirstOrDefaultAsync();
                if (invoice == null) throw new InvalidOperationException("Cannot dispense: invoice for prescription item not found");

                if (invoice.Status != Domain.Billing.InvoiceStatus.PAID)
                {
                    if (!req.AllowOnCredit)
                    {
                        throw new InvalidOperationException("Cannot dispense: invoice for prescription item is not paid");
                    }

                    // Check permission for dispensing on credit
                    if (!(_currentUserService.HasPermission("pharmacy.dispense.credit")))
                    {
                        throw new InvalidOperationException("Insufficient permissions to dispense on credit");
                    }

                    isOnCredit = true;

                    // record audit linking dispense to unpaid invoice
                    var userId = _currentUserService.UserId ?? Guid.Empty;
                    _db.BillingAudits.Add(new Domain.Billing.BillingAudit { UserId = userId, Action = "DispenseOnCredit", Details = $"Dispensed prescription item {item.Id} on credit for Invoice {invoice.InvoiceNumber}. Reason: {req.CreditReason}" });

                    // Create a DebtorEntry to track the debt
                    var debtor = new DebtorEntry
                    {
                        InvoiceId = invoice.Id,
                        SourceItemId = item.Id,
                        SourceType = "pharmacy",
                        AmountOwed = item.Price * (item.Quantity - item.DispensedQuantity),
                        Reason = req.CreditReason,
                        CreatedBy = userId
                    };
                    _db.DebtorEntries.Add(debtor);
                }
            }

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                if (item.InventoryItem.Stock < req.Quantity) throw new InvalidOperationException("Insufficient stock");

                // adjust inventory stock and reserved via service AdjustStock
                item.InventoryItem.Stock -= req.Quantity;
                item.DispensedQuantity += req.Quantity;
                item.InventoryItem.ReservedStock = Math.Max(0, item.InventoryItem.ReservedStock - req.Quantity);

                var log = new Domain.Pharmacy.DispenseLog { PrescriptionId = item.PrescriptionId, PrescriptionItemId = item.Id, DispensedBy = _currentUserService.UserId ?? Guid.Empty, Quantity = req.Quantity };
                _db.DispenseLogs.Add(log);

                // set prescription status if all items fully dispensed
                if (item.Prescription.Items.All(i => i.DispensedQuantity >= i.Quantity))
                {
                    item.Prescription.Status = Domain.Pharmacy.PrescriptionStatus.DISPENSED;
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                // publish outbox event for dispense
                var dispensedEvent = new HMS.API.Application.Pharmacy.Events.PrescriptionDispensedEvent { PrescriptionId = item.PrescriptionId, PrescriptionItemId = item.Id, DispensedBy = log.DispensedBy, Quantity = log.Quantity, DispensedAt = log.DispensedAt };
                var outboxDispensed = new OutboxMessage { Type = "PrescriptionDispensed", Content = JsonSerializer.Serialize(dispensedEvent), OccurredAt = DateTimeOffset.UtcNow };
                _db.OutboxMessages.Add(outboxDispensed);
                await _db.SaveChangesAsync();

                return new DispenseDto { Id = log.Id, PrescriptionId = log.PrescriptionId, PrescriptionItemId = log.PrescriptionItemId, DispensedAt = log.DispensedAt, DispensedBy = log.DispensedBy, Quantity = log.Quantity, IsOnCredit = isOnCredit, CreditReason = req.CreditReason };
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        // Update prescription header/details
        public async Task UpdatePrescriptionAsync(Guid id, Guid patientId, Guid? visitId)
        {
            var p = await _db.Prescriptions.SingleOrDefaultAsync(pr => pr.Id == id);
            if (p == null) throw new InvalidOperationException("Prescription not found");
            if (p.Status == Domain.Pharmacy.PrescriptionStatus.DISPENSED) throw new InvalidOperationException("Cannot edit a fully dispensed prescription");
            p.PatientId = patientId;
            p.VisitId = visitId;
            await _db.SaveChangesAsync();
        }

        // Replace/update prescription items
        public async Task UpdatePrescriptionItemsAsync(Guid id, List<CreatePrescriptionItem> items, bool allowIfDispensed = false)
        {
            var p = await _db.Prescriptions.Include(pr => pr.Items).ThenInclude(i => i.InventoryItem).SingleOrDefaultAsync(pr => pr.Id == id);
            if (p == null) throw new InvalidOperationException("Prescription not found");
            if (p.Status == Domain.Pharmacy.PrescriptionStatus.DISPENSED && !allowIfDispensed) throw new InvalidOperationException("Cannot edit items on a dispensed prescription");

            // release reserved stock for existing items
            foreach (var existing in p.Items.ToList())
            {
                var inv = existing.InventoryItem;
                if (inv != null)
                {
                    inv.ReservedStock = Math.Max(0, inv.ReservedStock - existing.Quantity);
                }
                _db.PrescriptionItems.Remove(existing);
            }

            var billingItems = new List<CreateInvoiceItemRequest>();
            foreach (var it in items)
            {
                var inv = await _db.InventoryItems.FindAsync(it.InventoryItemId);
                if (inv == null) throw new InvalidOperationException("Inventory item not found");
                var available = inv.Stock - inv.ReservedStock;
                if (available < it.Quantity) throw new InvalidOperationException("Insufficient available stock to reserve for prescription");

                var pi = new Domain.Pharmacy.PrescriptionItem { InventoryItem = inv, InventoryItemId = inv.Id, Quantity = it.Quantity, Price = inv.UnitPrice, Currency = inv.Currency };
                p.Items.Add(pi);
                inv.ReservedStock += it.Quantity;

                var reservation = new Domain.Pharmacy.Reservation { InventoryItemId = inv.Id, Quantity = it.Quantity, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30), PrescriptionItemId = pi.Id };
                _db.Reservations.Add(reservation);

                billingItems.Add(new CreateInvoiceItemRequest { Description = inv.Name, UnitPrice = inv.UnitPrice, Quantity = it.Quantity, SourceId = pi.Id, SourceType = "pharmacy" });
            }

            await _db.SaveChangesAsync();
        }

        // Implement AddNoteAsync
        public async Task AddNoteAsync(Guid prescriptionId, Guid itemId, string note)
        {
            var item = await _db.PrescriptionItems.Include(i => i.Prescription).SingleOrDefaultAsync(i => i.Id == itemId && i.PrescriptionId == prescriptionId);
            if (item == null) throw new InvalidOperationException("Prescription item not found");

            item.Notes = string.IsNullOrWhiteSpace(item.Notes) ? note : item.Notes + "\n" + note;
            _db.BillingAudits.Add(new Domain.Billing.BillingAudit { UserId = _currentUserService.UserId ?? Guid.Empty, Action = "PrescriptionNote", Details = $"Note added to prescription item {itemId}: {note}" });
            await _db.SaveChangesAsync();
        }

        public async Task CleanupExpiredReservationsAsync()
        {
            var now = DateTimeOffset.UtcNow;
            var expired = await _db.Reservations.Where(r => !r.Processed && r.ExpiresAt <= now).ToListAsync();
            foreach (var r in expired)
            {
                var inv = await _db.InventoryItems.FindAsync(r.InventoryItemId);
                if (inv != null)
                {
                    inv.ReservedStock = Math.Max(0, inv.ReservedStock - r.Quantity);
                }
                r.Processed = true;
            }
            await _db.SaveChangesAsync();
        }
    }
}