using System;

namespace HMS.API.Application.Profile.DTOs
{
    public class UserProfileDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }

        // Personal
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string OtherNames { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public DateTimeOffset? DateOfBirth { get; set; }

        // Contact
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PhotoUrl { get; set; } = string.Empty;

        // Employment
        public string StaffNumber { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public bool IsMedicalStaff { get; set; }

        // Audit
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
    }

    public class UpdateUserProfileRequest
    {
        // Nullable semantics: client may send subset; service will update only allowed fields.
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? OtherNames { get; set; }
        public string? Gender { get; set; }
        public DateTimeOffset? DateOfBirth { get; set; }

        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? PhotoUrl { get; set; }

        public string? StaffNumber { get; set; }
        public string? Department { get; set; }
        public string? JobTitle { get; set; }
        public bool? IsMedicalStaff { get; set; }
    }
}
