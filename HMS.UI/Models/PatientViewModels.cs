using HMS.UI.Models.Lab;
using HMS.UI.Models.Pharmacy;
using System;
using System.ComponentModel.DataAnnotations;

namespace HMS.UI.Models
{
    public class PagedResult<T>
    {
        public T[] Items { get; set; } = Array.Empty<T>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class VitalSignListItem
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public Guid VisitId { get; set; }
        public DateTimeOffset RecordedAt { get; set; }
        public decimal? Temperature { get; set; }
        public int? PulseRate { get; set; }
        public int? RespiratoryRate { get; set; }
        public int? SystolicBP { get; set; }
        public int? DiastolicBP { get; set; }
        public int? OxygenSaturation { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal? HeightCm { get; set; }
        public decimal? BMI { get; set; }
        public decimal? BloodSugar { get; set; }
        public string? Notes { get; set; }
        public Guid? RecordedByUserId { get; set; }
    }

    public class VisitDetailsViewModel
    {
        public VisitViewModel? Visit { get; set; }
        public PatientDetailsViewModel? Patient { get; set; }
        public VitalSignListItem[] RecentVitals { get; set; } = Array.Empty<VitalSignListItem>();
        public ConsultationViewModel[] Consultations { get; set; } = Array.Empty<ConsultationViewModel>();
        public HMS.UI.Models.Billing.InvoiceViewModel[] Invoices { get; set; } = Array.Empty<HMS.UI.Models.Billing.InvoiceViewModel>();
        public LabRequestViewModel[] LabRequests { get; set; } = Array.Empty<LabRequestViewModel>();
        public PrescriptionViewModel[] Prescriptions { get; set; } = Array.Empty<PrescriptionViewModel>();
    }

    public class PatientTimelineEntryViewModel
    {
        public DateTimeOffset Timestamp { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public string? Status { get; set; }
        public string? BadgeClass { get; set; }
        public string? IconClass { get; set; }
        public string? LinkUrl { get; set; }
        public string? LinkLabel { get; set; }
    }

    public class ListQueryControlsViewModel
    {
        public string Action { get; set; } = string.Empty;
        public string? Controller { get; set; }
        public Dictionary<string, string?> HiddenFields { get; set; } = new();
        public string? FilterLabel { get; set; }
        public string? FilterName { get; set; }
        public string? SelectedFilter { get; set; }
        public string[] FilterOptions { get; set; } = Array.Empty<string>();
        public string? SortLabel { get; set; }
        public string? SortName { get; set; }
        public string? SelectedSort { get; set; }
        public string[] SortOptions { get; set; } = Array.Empty<string>();
        public string? PageSizeLabel { get; set; }
        public string? PageSizeName { get; set; }
        public int SelectedPageSize { get; set; }
        public int[] PageSizeOptions { get; set; } = Array.Empty<int>();
        public string SubmitText { get; set; } = "Apply";
        public bool UseAjax { get; set; }
        public string? AjaxTarget { get; set; }
        public string AjaxSwap { get; set; } = "outerHTML";
        public string? AjaxExtraQuery { get; set; }
    }

    public class PagedNavigationViewModel
    {
        public string Action { get; set; } = string.Empty;
        public string? Controller { get; set; }
        public Dictionary<string, string?> RouteValues { get; set; } = new();
        public string PageParameterName { get; set; } = "page";
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public int PageSize { get; set; }
        public string SummaryLabel { get; set; } = "Showing";
        public bool UseAjax { get; set; }
        public string? AjaxTarget { get; set; }
        public string AjaxSwap { get; set; } = "outerHTML";
        public string? AjaxExtraQuery { get; set; }
    }

    public class PatientChartViewModel
    {
        public PatientDetailsViewModel Patient { get; set; } = new PatientDetailsViewModel();
        public VisitViewModel[] Visits { get; set; } = Array.Empty<VisitViewModel>();
        public VisitViewModel? SelectedVisit { get; set; }
        public VitalSignListItem[] RecentVitals { get; set; } = Array.Empty<VitalSignListItem>();
        public ConsultationViewModel[] Consultations { get; set; } = Array.Empty<ConsultationViewModel>();
        public HMS.UI.Models.Billing.InvoiceViewModel[] Invoices { get; set; } = Array.Empty<HMS.UI.Models.Billing.InvoiceViewModel>();
        public LabRequestViewModel[] LabRequests { get; set; } = Array.Empty<LabRequestViewModel>();
        public PrescriptionViewModel[] Prescriptions { get; set; } = Array.Empty<PrescriptionViewModel>();
        public PatientTimelineEntryViewModel[] Timeline { get; set; } = Array.Empty<PatientTimelineEntryViewModel>();
        public PagedResult<PatientTimelineEntryViewModel> TimelinePage { get; set; } = new PagedResult<PatientTimelineEntryViewModel>();
        public string TimelineCategory { get; set; } = "All";
        public string TimelineSort { get; set; } = "recent";
        public int TimelinePageNumber { get; set; } = 1;
        public int TimelinePageSize { get; set; } = 6;
    }

    public class EnterVitalsPageViewModel
    {
        public VitalSignViewModel Form { get; set; } = new VitalSignViewModel();
        public PatientDetailsViewModel? Patient { get; set; }
        public VisitViewModel? Visit { get; set; }
        public VitalSignListItem[] RecentVitals { get; set; } = Array.Empty<VitalSignListItem>();
    }

    public class ConsultationViewModel
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public Guid VisitId { get; set; }
        public Guid? DoctorId { get; set; }
        public DateTimeOffset ConsultationAt { get; set; }
        public DateTimeOffset? FollowUpAt { get; set; }
        public string? ChiefComplaint { get; set; }
        public string? HistoryOfPresentIllness { get; set; }
        public string? PhysicalExamination { get; set; }
        public string? DiagnosisCodes { get; set; }
        public string? Procedures { get; set; }
        public string? Notes { get; set; }
        public string? Status { get; set; }
    }

    public class CreateConsultationViewModel
    {
        public Guid PatientId { get; set; }
        public Guid VisitId { get; set; }
        public Guid? DoctorId { get; set; }
        public DateTimeOffset ConsultationAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? FollowUpAt { get; set; }
        public string? ChiefComplaint { get; set; }
        public string? HistoryOfPresentIllness { get; set; }
        public string? PhysicalExamination { get; set; }
        public string? DiagnosisCodes { get; set; }
        public string? Procedures { get; set; }
        public string? Notes { get; set; }
        public string? Status { get; set; }
    }

    public class PatientListItemViewModel
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string? MedicalRecordNumber { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }

    public class PatientDetailsViewModel : PatientListItemViewModel
    {
        public DateOnly DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string? AlternatePhone { get; set; }
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public string? Nationality { get; set; }
        public string? NationalIdNumber { get; set; }
        public string? BloodGroup { get; set; }
        public string? Genotype { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactRelationship { get; set; }
        public string? EmergencyContactPhone { get; set; }
        public string? InsuranceProvider { get; set; }
        public string? InsuranceNumber { get; set; }
        public string? Occupation { get; set; }
        public string? PhotoUrl { get; set; }
        public string? MaritalStatus { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public bool IsActive { get; set; }
    }

    public class PatientCreateViewModel
    {
        // include Id so same view model can be used for edit flows
        public Guid? Id { get; set; }
        [Required]
        [MaxLength(200)]
        public string FirstName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? MiddleName { get; set; }

        [Required]
        [MaxLength(200)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        public DateOnly DateOfBirth { get; set; }

        [MaxLength(50)]
        public string Gender { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(50)]
        public string? AlternatePhone { get; set; }

        [MaxLength(255)]
        [EmailAddress]
        public string? Email { get; set; }

        [MaxLength(200)]
        public string? MedicalRecordNumber { get; set; }

        // optional extended fields
        [MaxLength(200)] public string? AddressLine1 { get; set; }
        [MaxLength(200)] public string? AddressLine2 { get; set; }
        [MaxLength(100)] public string? City { get; set; }
        [MaxLength(100)] public string? State { get; set; }
        [MaxLength(50)] public string? PostalCode { get; set; }
        [MaxLength(100)] public string? Country { get; set; }

        [MaxLength(100)] public string? Nationality { get; set; }
        [MaxLength(100)] public string? NationalIdNumber { get; set; }
        [MaxLength(10)] public string? BloodGroup { get; set; }
        [MaxLength(10)] public string? Genotype { get; set; }

        [MaxLength(200)] public string? EmergencyContactName { get; set; }
        [MaxLength(100)] public string? EmergencyContactRelationship { get; set; }
        [MaxLength(50)] public string? EmergencyContactPhone { get; set; }

        [MaxLength(200)] public string? InsuranceProvider { get; set; }
        [MaxLength(200)] public string? InsuranceNumber { get; set; }

        [MaxLength(200)] public string? Occupation { get; set; }
        public bool IsActive { get; set; } = true;

       [MaxLength(50)] public string? MaritalStatus { get; set; }


        public string? PhotoUrl { get; set; }
    }

    public class VisitViewModel
    {
        public Guid Id { get; set; }
        public DateTimeOffset VisitAt { get; set; }
        public string VisitType { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public Guid PatientId { get; set; }
    }

    public class VisitCreateViewModel
    {
        public Guid? Id { get; set; }
        public Guid PatientId { get; set; }

        [Required]
        public DateTimeOffset VisitAt { get; set; } = DateTimeOffset.UtcNow;

        [MaxLength(100)]
        public string VisitType { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Notes { get; set; }
    }
}
