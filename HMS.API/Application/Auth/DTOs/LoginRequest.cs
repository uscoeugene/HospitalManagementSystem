using System;

namespace HMS.API.Application.Auth.DTOs
{
    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        // Optional tenant id (DEPRECATED): tenant context is resolved by middleware. If supplied it will be ignored when middleware resolves a tenant.
        public Guid? TenantId { get; set; }
    }
}