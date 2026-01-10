using System;

namespace HMS.API.Application.Auth.DTOs
{
    public class RefreshRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class RefreshResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
    }
}