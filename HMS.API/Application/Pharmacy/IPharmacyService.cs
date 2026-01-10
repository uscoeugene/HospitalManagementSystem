using System;
using System.Threading.Tasks;
using HMS.API.Application.Common;
using HMS.API.Application.Pharmacy.DTOs;

namespace HMS.API.Application.Pharmacy
{
    public interface IPharmacyService
    {
        Task<DrugDto[]> ListDrugsAsync();
        Task<DrugDto> CreateDrugAsync(DrugDto dto);

        Task<PrescriptionDto> CreatePrescriptionAsync(CreatePrescriptionRequest req);
        Task<PrescriptionDto?> GetPrescriptionAsync(Guid id);
        Task<PagedResult<PrescriptionDto>> ListPrescriptionsAsync(Guid? patientId = null, string? status = null, int page = 1, int pageSize = 20);

        Task<DispenseDto> DispenseAsync(DispenseRequest req);

        Task AddNoteAsync(Guid prescriptionId, Guid itemId, string note);

        Task CleanupExpiredReservationsAsync();
    }
}