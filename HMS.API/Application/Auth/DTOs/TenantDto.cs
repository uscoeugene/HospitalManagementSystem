using System;

namespace HMS.API.Application.Auth.DTOs
{
    public class TenantDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public string? LogoUrl { get; set; }
    }
}
