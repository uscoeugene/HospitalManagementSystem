using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HMS.API.Application.Billing;
using HMS.API.Application.Billing.DTOs;
using HMS.API.Application.Common;
using HMS.API.Application.Pharmacy.DTOs;
using HMS.API.Domain.Common;
using HMS.API.Domain.Billing;
using HMS.API.Domain.Pharmacy;
using HMS.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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

        public async Task<PrescriptionDto> CreatePrescriptionAsync(CreatePrescriptionRequest req)
        {
            var prescription = new Prescription
            {
                PatientId = req.PatientId,
                VisitId = req.VisitId,
                Status = PrescriptionStatus.IN_PHARMACY
            };

            foreach (var item in req.Items.Where(i => i.Quantity > 0 && (!string.IsNullOrWhiteSpace(i.MedicationName) || i.InventoryItemId.HasValue)))
            {
                var inventory = await ResolveInventoryAsync(item.InventoryItemId);
                var medicationName = ResolveMedicationName(item.MedicationName, inventory);

                if (string.IsNullOrWhiteSpace(medicationName))
                {
                    throw new InvalidOperationException("Medication name is required for each prescription item.");
                }

                var prescriptionItem = new PrescriptionItem
                {
                    InventoryItemId = inventory?.Id,
                    InventoryItem = inventory,
                    MedicationName = medicationName,
                    Dosage = item.Dosage?.Trim(),
                    Frequency = item.Frequency?.Trim(),
                    Quantity = item.Quantity,
                    Price = inventory?.UnitPrice ?? 0m,
                    Currency = inventory?.Currency ?? "NGN",
                    FulfillmentStatus = DetermineFulfillmentStatus(item.Quantity, 0, inventory),
                    ShortageReason = BuildShortageReason(item.Quantity, inventory)
                };

                // preserve billing preference
                prescriptionItem.ChargeSeparately = item.ChargeSeparately;

                prescription.Items.Add(prescriptionItem);
            }

            if (!prescription.Items.Any())
            {
                throw new InvalidOperationException("Add at least one prescription item.");
            }

            _db.Prescriptions.Add(prescription);
            await _db.SaveChangesAsync();

            return await BuildPrescriptionDtoAsync(prescription.Id) ?? throw new InvalidOperationException("Prescription could not be created.");
        }

        public async Task<PrescriptionDto?> GetPrescriptionAsync(Guid id)
        {
            return await BuildPrescriptionDtoAsync(id);
        }

        public async Task<PagedResult<PrescriptionDto>> ListPrescriptionsAsync(Guid? patientId = null, Guid? visitId = null, string? status = null, int page = 1, int pageSize = 20)
        {
            var query = _db.Prescriptions
                .AsNoTracking()
                .Include(pr => pr.Items)
                .ThenInclude(i => i.InventoryItem)
                .Where(pr => !pr.IsDeleted);

            if (patientId.HasValue)
            {
                query = query.Where(pr => pr.PatientId == patientId.Value);
            }

            if (visitId.HasValue)
            {
                query = query.Where(pr => pr.VisitId == visitId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<PrescriptionStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(pr => pr.Status == parsedStatus);
            }

            var total = await query.CountAsync();
            var prescriptions = await query
                .OrderByDescending(pr => pr.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = prescriptions.Select(MapPrescription).ToArray();

            return new PagedResult<PrescriptionDto>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<DispenseDto> DispenseAsync(DispenseRequest req)
        {
            var item = await _db.PrescriptionItems
                .Include(pi => pi.Prescription)
                .ThenInclude(p => p.Items)
                .Include(pi => pi.InventoryItem)
                .SingleOrDefaultAsync(pi => pi.Id == req.PrescriptionItemId);

            if (item == null)
            {
                throw new InvalidOperationException("Prescription item not found");
            }

            var inventory = await ResolveInventoryAsync(req.InventoryItemId ?? item.InventoryItemId);
            if (inventory == null)
            {
                item.FulfillmentStatus = PrescriptionItemStatus.OUT_OF_STOCK;
                item.ShortageReason = "No linked inventory item is available for dispensing.";
                await _db.SaveChangesAsync();
                throw new InvalidOperationException("No inventory item is linked. Pharmacist should reconcile or substitute before dispensing.");
            }

            if (inventory.Stock < req.Quantity)
            {
                item.InventoryItemId = inventory.Id;
                item.InventoryItem = inventory;
                item.FulfillmentStatus = PrescriptionItemStatus.OUT_OF_STOCK;
                item.ShortageReason = $"Only {inventory.Stock} unit(s) available for {inventory.Name}.";
                await _db.SaveChangesAsync();
                throw new InvalidOperationException("Insufficient stock for this dispense quantity.");
            }

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                item.InventoryItemId = inventory.Id;
                item.InventoryItem = inventory;
                item.Price = inventory.UnitPrice;
                item.Currency = inventory.Currency;

                var dispensedMedicationName = string.IsNullOrWhiteSpace(req.DispensedMedicationName)
                    ? inventory.Name
                    : req.DispensedMedicationName.Trim();

                item.IsSubstituted = !string.Equals(item.MedicationName, dispensedMedicationName, StringComparison.OrdinalIgnoreCase);
                item.SubstituteMedicationName = item.IsSubstituted ? dispensedMedicationName : null;
                item.ShortageReason = null;

                inventory.Stock -= req.Quantity;
                item.DispensedQuantity += req.Quantity;

                if (!string.IsNullOrWhiteSpace(req.Note))
                {
                    item.Notes = string.IsNullOrWhiteSpace(item.Notes)
                        ? req.Note.Trim()
                        : item.Notes + Environment.NewLine + req.Note.Trim();
                }

                item.FulfillmentStatus = item.DispensedQuantity >= item.Quantity
                    ? (item.IsSubstituted ? PrescriptionItemStatus.SUBSTITUTED : PrescriptionItemStatus.DISPENSED)
                    : PrescriptionItemStatus.PARTIALLY_DISPENSED;

                var log = new DispenseLog
                {
                    PrescriptionId = item.PrescriptionId,
                    PrescriptionItemId = item.Id,
                    InventoryItemId = inventory.Id,
                    MedicationName = dispensedMedicationName,
                    DispensedBy = _currentUserService.UserId ?? Guid.Empty,
                    Quantity = req.Quantity,
                    Notes = req.Note
                };
                _db.DispenseLogs.Add(log);
                await _db.SaveChangesAsync();

                // Billing flow: if item is marked to charge separately, create per-item invoice as before.
                // Otherwise, expect that the pharmacy has already created a grouped invoice for the prescription
                // and linked invoice items. If no grouped invoice exists, create it now for the whole prescription
                // excluding items marked ChargeSeparately.
                if (item.ChargeSeparately)
                {
                    var invoiceRequest = new CreateInvoiceFromLabRequest
                    {
                        PatientId = item.Prescription.PatientId,
                        VisitId = item.Prescription.VisitId,
                        Currency = inventory.Currency,
                        AllowOnCredit = req.AllowOnCredit,
                        CreditReason = req.CreditReason,
                        Items = new[]
                        {
                            new CreateInvoiceItemRequest
                            {
                                Description = dispensedMedicationName,
                                UnitPrice = inventory.UnitPrice,
                                Quantity = req.Quantity,
                                SourceId = item.Id,
                                SourceType = "pharmacy"
                            }
                        }
                    };

                    var invoice = await _billing.CreateInvoiceFromLabRequestAsync(invoiceRequest);
                    var invoiceItem = invoice.Items.FirstOrDefault(ii => ii.SourceId == item.Id && ii.SourceType == "pharmacy");
                    if (invoiceItem != null && !item.ChargeInvoiceItemId.HasValue)
                    {
                        item.ChargeInvoiceItemId = invoiceItem.Id;
                    }
                }
                else
                {
                    // find or create grouped invoice for the prescription
                    var prescriptionId = item.PrescriptionId;
                    // try to find an existing invoice that already contains invoice items for this prescription
                    var groupedInvoice = await _db.Invoices.Include(i => i.Items).Where(i => i.Items.Any(ii => ii.SourceType == "prescription" && ii.SourceId == prescriptionId) && !i.IsDeleted).OrderByDescending(i => i.CreatedAt).FirstOrDefaultAsync();
                    if (groupedInvoice == null)
                    {
                        // build invoice items for prescription items that are NOT ChargeSeparately
                        var groupItems = item.Prescription.Items.Where(pi => !pi.ChargeSeparately).Select(pi => new CreateInvoiceItemRequest
                        {
                            Description = pi.MedicationName,
                            UnitPrice = pi.Price,
                            Quantity = pi.Quantity,
                            SourceId = prescriptionId, // link at invoice item level to prescription id
                            SourceType = "prescription"
                        }).ToArray();

                        if (groupItems.Any())
                        {
                            var invReq = new CreateInvoiceFromLabRequest
                            {
                                PatientId = item.Prescription.PatientId,
                                VisitId = item.Prescription.VisitId,
                                Currency = inventory.Currency,
                                AllowOnCredit = req.AllowOnCredit,
                                CreditReason = req.CreditReason,
                                Items = groupItems
                            };

                            var inv = await _billing.CreateInvoiceFromLabRequestAsync(invReq);
                            groupedInvoice = await _db.Invoices.Include(i => i.Items).SingleOrDefaultAsync(i => i.Id == inv.Id);
                        }
                    }

                    // attempt to find invoice item for this individual prescription item
                    if (groupedInvoice != null)
                    {
                        var invItem = groupedInvoice.Items.FirstOrDefault(ii => ii.SourceType == "prescription" && ii.SourceId == prescriptionId && ii.Description == item.MedicationName && ii.UnitPrice == item.Price);
                        if (invItem != null && !item.ChargeInvoiceItemId.HasValue)
                        {
                            item.ChargeInvoiceItemId = invItem.Id;
                        }
                    }
                }

                item.Prescription.Status = item.Prescription.Items.All(i => i.DispensedQuantity >= i.Quantity)
                    ? PrescriptionStatus.DISPENSED
                    : PrescriptionStatus.IN_PHARMACY;

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                var dispensedEvent = new Events.PrescriptionDispensedEvent
                {
                    PrescriptionId = item.PrescriptionId,
                    PrescriptionItemId = item.Id,
                    DispensedBy = log.DispensedBy,
                    Quantity = log.Quantity,
                    DispensedAt = log.DispensedAt
                };

                _db.OutboxMessages.Add(new OutboxMessage
                {
                    Type = "PrescriptionDispensed",
                    Content = JsonSerializer.Serialize(dispensedEvent),
                    OccurredAt = DateTimeOffset.UtcNow
                });
                await _db.SaveChangesAsync();

                return new DispenseDto
                {
                    Id = log.Id,
                    PrescriptionId = log.PrescriptionId,
                    PrescriptionItemId = log.PrescriptionItemId,
                    DispensedAt = log.DispensedAt,
                    DispensedBy = log.DispensedBy,
                    Quantity = log.Quantity,
                    IsOnCredit = req.AllowOnCredit,
                    CreditReason = req.CreditReason
                };
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task AddNoteAsync(Guid prescriptionId, Guid itemId, string note)
        {
            var item = await _db.PrescriptionItems.Include(i => i.Prescription).SingleOrDefaultAsync(i => i.Id == itemId && i.PrescriptionId == prescriptionId);
            if (item == null) throw new InvalidOperationException("Prescription item not found");

            item.Notes = string.IsNullOrWhiteSpace(item.Notes) ? note : item.Notes + Environment.NewLine + note;
            _db.BillingAudits.Add(new BillingAudit
            {
                UserId = _currentUserService.UserId ?? Guid.Empty,
                Action = "PrescriptionNote",
                Details = $"Note added to prescription item {itemId}: {note}"
            });
            await _db.SaveChangesAsync();
        }

        public async Task ReconcilePrescriptionItemAsync(Guid prescriptionId, Guid itemId, ReconcilePrescriptionItemRequest request)
        {
            var item = await _db.PrescriptionItems
                .Include(i => i.Prescription)
                .Include(i => i.InventoryItem)
                .SingleOrDefaultAsync(i => i.Id == itemId && i.PrescriptionId == prescriptionId);

            if (item == null)
            {
                throw new InvalidOperationException("Prescription item not found");
            }

            var inventory = await ResolveInventoryAsync(request.InventoryItemId ?? item.InventoryItemId);
            if (request.InventoryItemId.HasValue && inventory == null)
            {
                throw new InvalidOperationException("Selected inventory item not found.");
            }

            if (inventory != null)
            {
                item.InventoryItemId = inventory.Id;
                item.InventoryItem = inventory;
                item.Price = inventory.UnitPrice;
                item.Currency = inventory.Currency;
            }

            if (!string.IsNullOrWhiteSpace(request.SubstituteMedicationName))
            {
                item.IsSubstituted = true;
                item.SubstituteMedicationName = request.SubstituteMedicationName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Note))
            {
                item.Notes = string.IsNullOrWhiteSpace(item.Notes)
                    ? request.Note.Trim()
                    : item.Notes + Environment.NewLine + request.Note.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                if (!Enum.TryParse<PrescriptionItemStatus>(request.Status, true, out var parsedStatus))
                {
                    throw new InvalidOperationException("Invalid prescription item status.");
                }

                item.FulfillmentStatus = parsedStatus;
            }
            else
            {
                item.FulfillmentStatus = DetermineFulfillmentStatus(item.Quantity, item.DispensedQuantity, inventory);
            }

            item.ShortageReason = item.FulfillmentStatus == PrescriptionItemStatus.OUT_OF_STOCK
                ? BuildShortageReason(item.Quantity - item.DispensedQuantity, inventory)
                : item.ShortageReason;

            await _db.SaveChangesAsync();
        }

        public async Task CleanupExpiredReservationsAsync()
        {
            var now = DateTimeOffset.UtcNow;
            var expired = await _db.Reservations.Where(r => !r.Processed && r.ExpiresAt <= now).ToListAsync();
            foreach (var reservation in expired)
            {
                reservation.Processed = true;
            }

            await _db.SaveChangesAsync();
        }

        public async Task UpdatePrescriptionAsync(Guid id, Guid patientId, Guid? visitId)
        {
            var prescription = await _db.Prescriptions.SingleOrDefaultAsync(pr => pr.Id == id);
            if (prescription == null) throw new InvalidOperationException("Prescription not found");
            if (prescription.Status == PrescriptionStatus.DISPENSED) throw new InvalidOperationException("Cannot edit a fully dispensed prescription");

            prescription.PatientId = patientId;
            prescription.VisitId = visitId;
            await _db.SaveChangesAsync();
        }

        public async Task UpdatePrescriptionItemsAsync(Guid id, List<CreatePrescriptionItem> items, bool allowIfDispensed = false)
        {
            var prescription = await _db.Prescriptions
                .Include(pr => pr.Items)
                .ThenInclude(i => i.InventoryItem)
                .SingleOrDefaultAsync(pr => pr.Id == id);

            if (prescription == null) throw new InvalidOperationException("Prescription not found");
            if (prescription.Status == PrescriptionStatus.DISPENSED && !allowIfDispensed) throw new InvalidOperationException("Cannot edit items on a dispensed prescription");
            if (prescription.Items.Any(i => i.DispensedQuantity > 0) && !allowIfDispensed) throw new InvalidOperationException("Cannot replace items after dispensing has started unless override is enabled.");

            foreach (var existing in prescription.Items.ToList())
            {
                _db.PrescriptionItems.Remove(existing);
            }

            foreach (var item in items.Where(i => i.Quantity > 0 && (!string.IsNullOrWhiteSpace(i.MedicationName) || i.InventoryItemId.HasValue)))
            {
                var inventory = await ResolveInventoryAsync(item.InventoryItemId);
                var medicationName = ResolveMedicationName(item.MedicationName, inventory);
                if (string.IsNullOrWhiteSpace(medicationName))
                {
                    throw new InvalidOperationException("Medication name is required for each prescription item.");
                }

                prescription.Items.Add(new PrescriptionItem
                {
                    InventoryItemId = inventory?.Id,
                    InventoryItem = inventory,
                    MedicationName = medicationName,
                    Dosage = item.Dosage?.Trim(),
                    Frequency = item.Frequency?.Trim(),
                    Quantity = item.Quantity,
                    Price = inventory?.UnitPrice ?? 0m,
                    Currency = inventory?.Currency ?? "NGN",
                    FulfillmentStatus = DetermineFulfillmentStatus(item.Quantity, 0, inventory),
                    ShortageReason = BuildShortageReason(item.Quantity, inventory)
                });
            }

            if (!prescription.Items.Any())
            {
                throw new InvalidOperationException("Add at least one prescription item.");
            }

            prescription.Status = PrescriptionStatus.IN_PHARMACY;
            await _db.SaveChangesAsync();
        }

        private async Task<InventoryItem?> ResolveInventoryAsync(Guid? inventoryItemId)
        {
            if (!inventoryItemId.HasValue)
            {
                return null;
            }

            return await _db.InventoryItems.SingleOrDefaultAsync(i => i.Id == inventoryItemId.Value);
        }

        private static string ResolveMedicationName(string? requestedName, InventoryItem? inventory)
        {
            if (!string.IsNullOrWhiteSpace(requestedName))
            {
                return requestedName.Trim();
            }

            return inventory?.Name ?? string.Empty;
        }

        private static PrescriptionItemStatus DetermineFulfillmentStatus(int quantity, int dispensedQuantity, InventoryItem? inventory)
        {
            if (dispensedQuantity >= quantity && quantity > 0)
            {
                return PrescriptionItemStatus.DISPENSED;
            }

            if (dispensedQuantity > 0)
            {
                return PrescriptionItemStatus.PARTIALLY_DISPENSED;
            }

            if (inventory == null)
            {
                return PrescriptionItemStatus.PENDING;
            }

            return inventory.Stock >= quantity ? PrescriptionItemStatus.READY : PrescriptionItemStatus.OUT_OF_STOCK;
        }

        private static string? BuildShortageReason(int quantityNeeded, InventoryItem? inventory)
        {
            if (inventory == null)
            {
                return null;
            }

            return inventory.Stock >= quantityNeeded
                ? null
                : $"Requested quantity is {quantityNeeded}, but only {inventory.Stock} unit(s) are in stock.";
        }

        private async Task<PrescriptionDto?> BuildPrescriptionDtoAsync(Guid id)
        {
            var prescription = await _db.Prescriptions
                .Include(pr => pr.Items)
                .ThenInclude(i => i.InventoryItem)
                .AsNoTracking()
                .SingleOrDefaultAsync(pr => pr.Id == id);

            return prescription == null ? null : MapPrescription(prescription);
        }

        private static PrescriptionDto MapPrescription(Prescription prescription)
        {
            return new PrescriptionDto
            {
                Id = prescription.Id,
                PatientId = prescription.PatientId,
                VisitId = prescription.VisitId,
                Status = prescription.Status.ToString(),
                Items = (prescription.Items ?? new List<PrescriptionItem>()).Select(item => new PrescriptionItemDto
                {
                    Id = item.Id,
                    InventoryItemId = item.InventoryItemId,
                    MedicationName = item.MedicationName ?? string.Empty,
                    InventoryItemName = item.InventoryItem?.Name,
                    Dosage = item.Dosage,
                    Frequency = item.Frequency,
                    Quantity = item.Quantity,
                    DispensedQuantity = item.DispensedQuantity,
                    Price = item.Price,
                    Currency = item.Currency,
                    Notes = item.Notes,
                    Status = item.FulfillmentStatus.ToString(),
                    ShortageReason = item.ShortageReason,
                    IsSubstituted = item.IsSubstituted,
                    SubstituteMedicationName = item.SubstituteMedicationName,
                    AvailableStock = item.InventoryItem?.Stock
                }).ToArray()
            };
        }
    }
}
