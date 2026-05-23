using System;
using System.ComponentModel.DataAnnotations;

namespace HMS.API.Application.Patient.DTOs
{
    public class CreateConsultationRequest
    {
        [Required]
        public Guid PatientId { get; set; }

        [Required]
        public Guid VisitId { get; set; }

        public Guid? DoctorId { get; set; }

        [Required]
        public DateTimeOffset ConsultationAt { get; set; }

        public DateTimeOffset? FollowUpAt { get; set; }

        [MaxLength(1000)] public string? ChiefComplaint { get; set; }
        [MaxLength(4000)] public string? HistoryOfPresentIllness { get; set; }
        [MaxLength(4000)] public string? PhysicalExamination { get; set; }
        [MaxLength(1000)] public string? DiagnosisCodes { get; set; }
        [MaxLength(1000)] public string? Procedures { get; set; }
        [MaxLength(4000)] public string? Notes { get; set; }
        [MaxLength(50)] public string? Status { get; set; }
    }

    public class ConsultationResponse
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
}
