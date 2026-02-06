using System.Threading.Tasks;
using HMS.API.Application.Reporting;

namespace HMS.API.Application.Patient
{
    public interface IPatientReportService : IReportService
    {
        Task<PagedReportResult<PatientSummaryReportDto>> GetPatientSummaryAsync(int page = 1, int pageSize = 50);
    }
}
