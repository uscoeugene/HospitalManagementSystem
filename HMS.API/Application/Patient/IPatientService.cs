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
        Task<VisitResponse?> GetVisitAsync(Guid id);
        Task<VisitResponse[]> ListVisitsForPatientAsync(Guid patientId);
        Task<VisitResponse> UpdateVisitAsync(Guid id, AddVisitRequest request);
        Task DeleteVisitAsync(Guid id);

        Task<PagedResult<PatientResponse>> ListPatientsAsync(string? search, int page = 1, int pageSize = 20);

        Task<PatientResponse> UpdatePatientAsync(Guid id, RegisterPatientRequest request);

        // enhanced duplicate detection: accepts threshold, dob tolerance in days, MRN prefix length to consider
        Task<DuplicateCandidateDto[]> FindPossibleDuplicatesAsync(string query, double threshold = 0.75, int dobToleranceDays = 365, int mrnPrefixLength = 4);

        Task<MergePatientsResult> MergePatientsAsync(MergePatientsRequest request);

        // Vital signs
        Task<VitalSignResponse> AddVitalSignAsync(Guid patientId, CreateVitalSignRequest request);
        Task<VitalSignResponse?> GetVitalSignAsync(Guid id);
        Task<VitalSignResponse[]> ListVitalSignsForVisitAsync(Guid visitId);
        Task<VitalSignResponse> UpdateVitalSignAsync(Guid id, CreateVitalSignRequest request);
    }
}