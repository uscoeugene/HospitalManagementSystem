using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Auth
{
    public class RefreshToken : BaseEntity
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        // Store hash of the token instead of raw token
        public string TokenHash { get; set; } = string.Empty;

        public DateTimeOffset ExpiresAt { get; set; }

        public bool IsRevoked { get; set; } = false;

        public DateTimeOffset? RevokedAt { get; set; }
    }
}