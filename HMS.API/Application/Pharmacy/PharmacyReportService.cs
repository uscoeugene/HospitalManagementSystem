using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HMS.API.Application.Pharmacy
{
    public class PharmacyReportService : IPharmacyReportService
    {
        private readonly HmsDbContext _db;

        public PharmacyReportService(HmsDbContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<StockShortageDto>> GetStockShortagesAsync(int threshold = 5)
        {
            var q = _db.InventoryItems.AsNoTracking().Where(d => d.Stock - d.ReservedStock <= threshold);
            var data = await q.Select(d => new StockShortageDto { InventoryItemId = d.Id, InventoryItemCode = d.Code, InventoryItemName = d.Name, Stock = d.Stock, Reserved = d.ReservedStock }).ToListAsync();
            return data;
        }

        public async Task<IEnumerable<DailyDispenseDto>> GetDailyDispensesAsync(int daysBack = 30)
        {
            var since = DateTimeOffset.UtcNow.AddDays(-daysBack);
            var q = _db.DispenseLogs.AsNoTracking().Where(d => d.DispensedAt >= since && !d.IsDeleted);
            var data = await q.GroupBy(d => d.DispensedAt.Date)
                              .Select(g => new DailyDispenseDto { Date = g.Key, DispensedCount = g.Count() })
                              .OrderBy(d => d.Date)
                              .ToListAsync();
            return data;
        }

        public async Task<IEnumerable<InventoryRevenueDto>> GetRevenuePerInventoryAsync(int monthsBack = 6)
        {
            var since = DateTimeOffset.UtcNow.AddMonths(-monthsBack);
            // Aggregate revenue from prescription items referencing inventory items
            var q = _db.PrescriptionItems.AsNoTracking().Where(pi => pi.CreatedAt >= since && !pi.IsDeleted).Include(pi => pi.InventoryItem);

            var data = await q.GroupBy(pi => new { pi.InventoryItemId, pi.InventoryItem!.Name })
                              .Select(g => new InventoryRevenueDto { InventoryItemId = g.Key.InventoryItemId, InventoryItemName = g.Key.Name, Revenue = g.Sum(pi => pi.Price * pi.Quantity) })
                              .OrderByDescending(r => r.Revenue)
                              .ToListAsync();
            return data;
        }
    }
}
