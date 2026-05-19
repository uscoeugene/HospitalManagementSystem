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
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public DateOnly DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string? MaritalStatus { get; set; }
        public string? Phone { get; set; }
        public string? AlternatePhone { get; set; }
        public string? Email { get; set; }

        // Address
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }

        public string? Nationality { get; set; }
        public string? NationalIdNumber { get; set; }

        // Clinical
        public string? BloodGroup { get; set; }
        public string? Genotype { get; set; }

        // Emergency contact
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactRelationship { get; set; }
        public string? EmergencyContactPhone { get; set; }

        // Insurance
        public string? InsuranceProvider { get; set; }
        public string? InsuranceNumber { get; set; }

        public string? Occupation { get; set; }
        public string? PhotoUrl { get; set; }

        public bool IsActive { get; set; } = true;

        // visits
        public ICollection<Visit> Visits { get; set; } = new List<Visit>();
    }
}