using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Profile
{
    /// <summary>
    /// Aggregate root for user profile data.
    /// Id is the PK (GUID). UserId is the canonical identifier issued by Auth Service and must be unique.
    /// </summary>
    public class UserProfile : BaseEntity
    {
        // Canonical identifier from Auth Service (GUID). Must be unique across profiles.
        public Guid UserId { get; set; }

        // Personal details
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

        // Employment / profile metadata
        public string StaffNumber { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public bool IsMedicalStaff { get; set; }

        // Audit fields (CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, etc.) are inherited from BaseEntity
    }
}
