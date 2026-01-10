using System;
using System.Collections.Generic;

namespace HMS.API.Application.Auth.DTOs
{
    public class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        public Guid UserId { get; set; }
        public IEnumerable<string> Permissions { get; set; } = Array.Empty<string>();
    }
}