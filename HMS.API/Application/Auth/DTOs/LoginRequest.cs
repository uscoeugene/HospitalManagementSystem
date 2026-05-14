using System;

namespace HMS.API.Application.Auth.DTOs
{
    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        // Optional tenant id for tenant-scoped login (can also be provided via X-Tenant-Id header)
        public Guid? TenantId { get; set; }
    }
}