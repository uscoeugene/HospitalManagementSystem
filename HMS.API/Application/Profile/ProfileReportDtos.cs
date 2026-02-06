using System;
using HMS.API.Application.Reporting;

namespace HMS.API.Application.Profile
{
    public class ProfileSummaryDto : ReportDtoBase
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
