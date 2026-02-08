using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Auth
{
    public class LocalUser : BaseEntity
    {
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? Email { get; set; }
        public Guid? TenantId { get; set; }
        public bool IsLocked { get; set; }
        public DateTimeOffset? LockedUntil { get; set; }
    }
}
