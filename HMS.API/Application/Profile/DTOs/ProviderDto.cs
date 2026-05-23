using System;

namespace HMS.API.Application.Profile.DTOs
{
    public class ProviderDto
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public string PhotoUrl { get; set; } = string.Empty;
        public bool IsMedicalStaff { get; set; }
        public bool IsDoctor { get; set; }
    }
}
