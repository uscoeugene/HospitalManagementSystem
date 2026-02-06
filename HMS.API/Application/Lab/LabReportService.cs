using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HMS.API.Application.Lab
{
    public class LabReportService : ILabReportService
    {
        private readonly HmsDbContext _db;

        public LabReportService(HmsDbContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<LabStatusBreakdownDto>> GetRequestStatusBreakdownAsync()
        {
            var q = _db.LabRequests.AsNoTracking().Where(r => !r.IsDeleted);
            var data = await q.GroupBy(r => r.Status.ToString())
                              .Select(g => new LabStatusBreakdownDto { Status = g.Key, Count = g.Count() })
                              .ToListAsync();
            return data;
        }

        public async Task<IEnumerable<LabTurnaroundDto>> GetTurnaroundTimesAsync(int recent = 100)
        {
            var q = _db.LabRequests.AsNoTracking().Where(r => !r.IsDeleted).OrderByDescending(r => r.CreatedAt).Take(recent);
            var data = await q.Select(r => new LabTurnaroundDto { LabRequestId = r.Id, TurnaroundHours = EF.Functions.DateDiffHour(r.CreatedAt, r.UpdatedAt ?? r.CreatedAt) }).ToListAsync();
            return data;
        }

        public async Task<IEnumerable<TopTestDto>> GetTopTestsAsync(int top = 10)
        {
            var q = _db.LabRequestItems.AsNoTracking().Include(i => i.LabTest);
            var data = await q.GroupBy(i => new { i.LabTestId, i.LabTest!.Name })
                              .Select(g => new TopTestDto { TestId = g.Key.LabTestId, TestName = g.Key.Name, Requests = g.Count() })
                              .OrderByDescending(t => t.Requests)
                              .Take(top)
                              .ToListAsync();
            return data;
        }
    }
}
