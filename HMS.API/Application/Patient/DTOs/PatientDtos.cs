using System;
using System.ComponentModel.DataAnnotations;

namespace HMS.API.Application.Patient.DTOs
{
    public class RegisterPatientRequest
    {
        [Required]
        [MaxLength(200)]
        public string FirstName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? MiddleName { get; set; }

        [Required]
        [MaxLength(200)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public DateOnly DateOfBirth { get; set; }

        [MaxLength(50)]
        public string Gender { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Phone { get; set; }
        [MaxLength(50)]
        public string? AlternatePhone { get; set; }

        [MaxLength(200)]
        public string? AddressLine1 { get; set; }
        [MaxLength(200)]
        public string? AddressLine2 { get; set; }
        [MaxLength(100)]
        public string? City { get; set; }
        [MaxLength(100)]
        public string? State { get; set; }
        [MaxLength(50)]
        public string? PostalCode { get; set; }
        [MaxLength(100)]
        public string? Country { get; set; }

        [MaxLength(50)]
        public string? MaritalStatus { get; set; }

        [MaxLength(100)]
        public string? Nationality { get; set; }

        [MaxLength(100)]
        public string? NationalIdNumber { get; set; }

        [MaxLength(10)]
        public string? BloodGroup { get; set; }
        [MaxLength(10)]
        public string? Genotype { get; set; }

        [MaxLength(200)]
        public string? EmergencyContactName { get; set; }
        [MaxLength(100)]
        public string? EmergencyContactRelationship { get; set; }
        [MaxLength(50)]
        public string? EmergencyContactPhone { get; set; }

        [MaxLength(200)]
        public string? InsuranceProvider { get; set; }
        [MaxLength(200)]
        public string? InsuranceNumber { get; set; }

        [MaxLength(200)]
        public string? Occupation { get; set; }

        [MaxLength(1000)]
        public string? PhotoUrl { get; set; }

        // allow caller to set initial active state (defaults to true in DB)
        public bool IsActive { get; set; } = true;

        [MaxLength(255)]
        public string? Email { get; set; }

        [MaxLength(200)]
        public string? MedicalRecordNumber { get; set; }
    }

    public class PatientResponse
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public DateOnly DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? AlternatePhone { get; set; }
        public string? Email { get; set; }
        public string? MedicalRecordNumber { get; set; }
        public string? MaritalStatus { get; set; }

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
        public bool IsActive { get; set; }
    }

    public class AddVisitRequest
    {
        [Required]
        public DateTimeOffset VisitAt { get; set; }

        [MaxLength(100)]
        public string VisitType { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Notes { get; set; }
    }

    public class VisitResponse
    {
        public Guid Id { get; set; }
        public DateTimeOffset VisitAt { get; set; }
        public string VisitType { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
}