using System.Linq;
using System.Threading.Tasks;
using HMS.API.Application.Reporting;
using HMS.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HMS.API.Application.Patient
{
    public class PatientReportService : IPatientReportService
    {
        private readonly HmsDbContext _db;

        public PatientReportService(HmsDbContext db)
        {
            _db = db;
        }

        public async Task<PagedReportResult<PatientSummaryReportDto>> GetPatientSummaryAsync(int page = 1, int pageSize = 50)
        {
            var q = _db.Patients.AsNoTracking().Where(p => !p.IsDeleted);
            var total = await q.CountAsync();
            var items = await q.OrderBy(p => p.LastName).Skip((page - 1) * pageSize).Take(pageSize).Select(p => new PatientSummaryReportDto
            {
                Id = p.Id,
                FullName = p.FirstName + " " + p.LastName,
                DateOfBirth = p.DateOfBirth,
                Gender = p.Gender,
                MedicalRecordNumber = p.MedicalRecordNumber,
                VisitCount = p.Visits.Count,
                LastVisit = p.Visits.OrderByDescending(v => v.VisitAt).Select(v => (DateTimeOffset?)v.VisitAt).FirstOrDefault()
            }).ToArrayAsync();

            return new PagedReportResult<PatientSummaryReportDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
        }
    }
}
