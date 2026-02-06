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
            var q = _db.Drugs.AsNoTracking().Where(d => d.Stock - d.ReservedStock <= threshold);
            var data = await q.Select(d => new StockShortageDto { DrugId = d.Id, DrugCode = d.Code, DrugName = d.Name, Stock = d.Stock, Reserved = d.ReservedStock }).ToListAsync();
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

        public async Task<IEnumerable<DrugRevenueDto>> GetRevenuePerDrugAsync(int monthsBack = 6)
        {
            var since = DateTimeOffset.UtcNow.AddMonths(-monthsBack);
            // Aggregate revenue from prescription items referencing drugs
            var q = _db.PrescriptionItems.AsNoTracking().Where(pi => pi.CreatedAt >= since && !pi.IsDeleted).Include(pi => pi.Drug);

            var data = await q.GroupBy(pi => new { pi.DrugId, pi.Drug!.Name })
                              .Select(g => new DrugRevenueDto { DrugId = g.Key.DrugId, DrugName = g.Key.Name, Revenue = g.Sum(pi => pi.Price * pi.Quantity) })
                              .OrderByDescending(r => r.Revenue)
                              .ToListAsync();
            return data;
        }
    }
}
