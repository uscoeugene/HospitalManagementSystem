using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Patient
{
    public class Consultation : BaseEntity
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

        public string Status { get; set; } = "Pending";

        public Visit? Visit { get; set; }
    }
}
