using System.Collections.Generic;
using System.Threading.Tasks;

namespace HMS.API.Application.Lab
{
    public interface ILabReportService
    {
        Task<IEnumerable<LabStatusBreakdownDto>> GetRequestStatusBreakdownAsync();
        Task<IEnumerable<LabTurnaroundDto>> GetTurnaroundTimesAsync(int recent = 100);
        Task<IEnumerable<TopTestDto>> GetTopTestsAsync(int top = 10);
    }
}
