using System;

namespace HMS.API.Application.Patient.DTOs
{
    public class DuplicateCandidateDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTimeOffset DateOfBirth { get; set; }
        public string? MedicalRecordNumber { get; set; }
        public double Similarity { get; set; }
    }

    public class MergePatientsRequest
    {
        public Guid TargetPatientId { get; set; }
        public Guid SourcePatientId { get; set; }
        // fields to prefer from source
        public bool PreferSourceName { get; set; } = false;
        public bool PreferSourceContact { get; set; } = false;
    }

    public class MergePatientsResult
    {
        public Guid TargetPatientId { get; set; }
        public Guid[] MergedSourceIds { get; set; } = Array.Empty<Guid>();
    }
}