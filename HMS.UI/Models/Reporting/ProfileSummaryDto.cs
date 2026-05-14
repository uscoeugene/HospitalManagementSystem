using System;

namespace HMS.UI.Models.Reporting
{
    public class ProfileSummaryDto
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public bool IsMedicalStaff { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
