using System;
using System.Threading.Tasks;
using HMS.API.Application.Patient.DTOs;
using HMS.API.Application.Common;

namespace HMS.API.Application.Patient
{
    public interface IPatientService
    {
        Task<PatientResponse> RegisterPatientAsync(RegisterPatientRequest request);
        Task<PatientResponse?> GetPatientAsync(Guid id);
        Task<VisitResponse> AddVisitAsync(Guid patientId, AddVisitRequest request);

        Task<PagedResult<PatientResponse>> ListPatientsAsync(string? search, int page = 1, int pageSize = 20);

        // enhanced duplicate detection: accepts threshold, dob tolerance in days, MRN prefix length to consider
        Task<DuplicateCandidateDto[]> FindPossibleDuplicatesAsync(string query, double threshold = 0.75, int dobToleranceDays = 365, int mrnPrefixLength = 4);

        Task<MergePatientsResult> MergePatientsAsync(MergePatientsRequest request);
    }
}