using System;
using System.Collections.Generic;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Patient
{
    public class Patient : BaseEntity
    {
        // Global unique patient identifier (GUID) in addition to local Id
        public string MedicalRecordNumber { get; set; } = string.Empty; // optional external MRN

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTimeOffset DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }

        // visits
        public ICollection<Visit> Visits { get; set; } = new List<Visit>();
    }
}