using System;
using System.Threading.Tasks;
using HMS.API.Application.Common;
using HMS.API.Application.Lab.DTOs;

namespace HMS.API.Application.Lab
{
    public interface ILabService
    {
        Task<LabTestDto[]> ListTestsAsync();
        Task<LabTestDto> CreateTestAsync(LabTestDto dto);

        Task<LabRequestDto> CreateRequestAsync(CreateLabRequest request);
        Task<LabRequestDto?> GetRequestAsync(Guid id);
        Task<PagedResult<LabRequestDto>> ListRequestsAsync(Guid? patientId = null, string? status = null, int page = 1, int pageSize = 20);
    }
}