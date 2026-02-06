using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HMS.API.Application.Billing
{
    public class BillingReportService : IBillingReportService
    {
        private readonly HmsDbContext _db;

        public BillingReportService(HmsDbContext db)
        {
            _db = db;
        }

        public async Task<BillingSummaryKpiDto> GetSummaryKpiAsync()
        {
            var q = _db.Invoices.AsNoTracking().Where(i => !i.IsDeleted);
            var totalRevenue = await q.SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;
            var invoiceCount = await q.CountAsync();
            var paidCount = await q.CountAsync(i => i.Status == Domain.Billing.InvoiceStatus.PAID);
            var unpaidCount = await q.CountAsync(i => i.Status == Domain.Billing.InvoiceStatus.UNPAID);
            var average = invoiceCount > 0 ? totalRevenue / invoiceCount : 0m;

            return new BillingSummaryKpiDto { TotalRevenue = totalRevenue, InvoiceCount = invoiceCount, PaidCount = paidCount, UnpaidCount = unpaidCount, AverageInvoice = average };
        }

        public async Task<IEnumerable<MonthlyRevenueDto>> GetMonthlyRevenueAsync(int monthsBack)
        {
            var since = DateTimeOffset.UtcNow.AddMonths(-monthsBack);
            var q = _db.Invoices.AsNoTracking().Where(i => i.CreatedAt >= since && !i.IsDeleted);

            var data = await q.GroupBy(i => new { Year = i.CreatedAt.Year, Month = i.CreatedAt.Month })
                              .Select(g => new MonthlyRevenueDto { Year = g.Key.Year, Month = g.Key.Month, Revenue = g.Sum(i => i.TotalAmount) })
                              .OrderBy(r => r.Year).ThenBy(r => r.Month)
                              .ToListAsync();

            return data;
        }

        public async Task<IEnumerable<StatusBreakdownDto>> GetInvoiceStatusBreakdownAsync()
        {
            var q = _db.Invoices.AsNoTracking().Where(i => !i.IsDeleted);
            var data = await q.GroupBy(i => i.Status.ToString())
                              .Select(g => new StatusBreakdownDto { Status = g.Key, Count = g.Count() })
                              .ToListAsync();
            return data;
        }

        public async Task<IEnumerable<DailyRevenueDto>> GetDailyRevenueAsync(int daysBack)
        {
            var since = DateTimeOffset.UtcNow.AddDays(-daysBack);
            var q = _db.Invoices.AsNoTracking().Where(i => i.CreatedAt >= since && !i.IsDeleted);

            var data = await q.GroupBy(i => i.CreatedAt.Date)
                              .Select(g => new DailyRevenueDto { Date = g.Key, Revenue = g.Sum(i => i.TotalAmount), Invoices = g.Count() })
                              .OrderBy(d => d.Date)
                              .ToListAsync();

            return data;
        }

        public async Task<IEnumerable<TopPatientDto>> GetTopPayingPatientsAsync(int top = 10)
        {
            // Join payments/invoices to aggregate by patient
            var q = _db.InvoicePayments.AsNoTracking().Where(p => !p.IsDeleted).Include(p => p.Invoice);

            var data = await q.GroupBy(p => p.Invoice.PatientId)
                              .Select(g => new { PatientId = g.Key, TotalPaid = g.Sum(p => p.Amount), Count = g.Count() })
                              .OrderByDescending(x => x.TotalPaid)
                              .Take(top)
                              .ToListAsync();

            // Map to DTOs; load patient names minimally
            var patientIds = data.Select(d => d.PatientId).ToArray();
            var patients = await _db.Patients.Where(pt => patientIds.Contains(pt.Id)).ToDictionaryAsync(pt => pt.Id, pt => pt.FirstName + " " + pt.LastName);

            return data.Select(d => new TopPatientDto { PatientId = d.PatientId, PatientName = patients.TryGetValue(d.PatientId, out var n) ? n : string.Empty, TotalPaid = d.TotalPaid, PaymentsCount = d.Count }).ToList();
        }

        public async Task<IEnumerable<RefundReportDto>> GetRecentRefundsAsync(int daysBack = 30)
        {
            var since = DateTimeOffset.UtcNow.AddDays(-daysBack);
            var q = _db.Refunds.AsNoTracking().Where(r => r.CreatedAt >= since && !r.IsDeleted).Include(r => r.Payment);

            var data = await q.Select(r => new RefundReportDto { RefundId = r.Id, PaymentId = r.PaymentId, InvoiceId = r.Payment != null ? r.Payment.InvoiceId : Guid.Empty, Amount = r.Amount, RefundedAt = r.CreatedAt, Reason = r.Reason }).ToListAsync();

            return data;
        }
    }
}
