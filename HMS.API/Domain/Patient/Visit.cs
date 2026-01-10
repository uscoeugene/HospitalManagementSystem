using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Patient
{
    public class Visit : BaseEntity
    {
        public Guid PatientId { get; set; }
        public Patient Patient { get; set; } = null!;

        public DateTimeOffset VisitAt { get; set; } = DateTimeOffset.UtcNow;

        public string VisitType { get; set; } = string.Empty; // e.g., outpatient, inpatient, emergency

        public string Notes { get; set; } = string.Empty;
    }
}