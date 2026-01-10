using System;
using System.ComponentModel.DataAnnotations;

namespace HMS.API.Application.Patient.DTOs
{
    public class RegisterPatientRequest
    {
        [Required]
        [MaxLength(200)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public DateTimeOffset DateOfBirth { get; set; }

        [MaxLength(50)]
        public string Gender { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(255)]
        public string? Email { get; set; }

        [MaxLength(200)]
        public string? MedicalRecordNumber { get; set; }
    }

    public class PatientResponse
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTimeOffset DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? MedicalRecordNumber { get; set; }
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