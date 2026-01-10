using System;
using HMS.API.Domain.Common;
using System.Collections.Generic;

namespace HMS.API.Domain.Auth
{
    public class User : BaseEntity
    {
        public string Username { get; set; } = string.Empty;

        // Password is stored as hashed password + salt in PasswordHash
        public string PasswordHash { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public bool IsLocked { get; set; } = false;

        public DateTimeOffset? LockedUntil { get; set; }

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    }
}