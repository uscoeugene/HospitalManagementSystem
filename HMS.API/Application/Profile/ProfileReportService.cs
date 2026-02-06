using System.Linq;
using System.Threading.Tasks;
using HMS.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HMS.API.Application.Profile
{
    public class ProfileReportService : IProfileReportService
    {
        private readonly HmsDbContext _db;

        public ProfileReportService(HmsDbContext db)
        {
            _db = db;
        }

        public async Task<HMS.API.Application.Reporting.PagedReportResult<ProfileSummaryDto>> GetProfileSummaryAsync(int page = 1, int pageSize = 50)
        {
            var q = _db.UserProfiles.AsNoTracking();
            var total = await q.CountAsync();
            var items = await q.OrderBy(p => p.LastName).Skip((page - 1) * pageSize).Take(pageSize).Select(p => new ProfileSummaryDto
            {
                UserId = p.UserId,
                FullName = p.FirstName + " " + p.LastName,
                Email = p.Email,
                Department = p.Department,
                JobTitle = p.JobTitle,
                IsMedicalStaff = p.IsMedicalStaff,
                UpdatedAt = p.UpdatedAt
            }).ToArrayAsync();

            return new HMS.API.Application.Reporting.PagedReportResult<ProfileSummaryDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
        }
    }
}
