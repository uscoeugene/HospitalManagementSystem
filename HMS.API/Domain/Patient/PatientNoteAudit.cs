using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Patient
{
    public class PatientNoteAudit : BaseEntity
    {
        public string EntityType { get; set; } = string.Empty; // Visit, Consultation
        public Guid EntityId { get; set; }

        public Guid PatientId { get; set; }
        public Guid? VisitId { get; set; }

        public string ChangeType { get; set; } = string.Empty; // Created, Updated, Deleted
        public string? Details { get; set; }
        public Guid PerformedBy { get; set; }
        public DateTimeOffset PerformedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
