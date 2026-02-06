using System.Threading.Tasks;
using HMS.API.Application.Reporting;

namespace HMS.API.Application.Profile
{
    public interface IProfileReportService : IReportService
    {
        Task<PagedReportResult<ProfileSummaryDto>> GetProfileSummaryAsync(int page = 1, int pageSize = 50);
    }
}
