using System;

namespace HMS.API.Application.Sync.DTOs
{
    public class UserProfileSyncDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string OtherNames { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public DateTimeOffset? DateOfBirth { get; set; }

        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PhotoUrl { get; set; } = string.Empty;

        public string StaffNumber { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public bool IsMedicalStaff { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public Guid? UpdatedByUserId { get; set; }

        // Sync + audit
        public bool IsSynced { get; set; }
        public long SyncVersion { get; set; }
    }
}
