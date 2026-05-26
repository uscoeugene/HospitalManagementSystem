using System;
using System.Collections.Generic;

namespace HMS.UI.Models.Auth
{
    public class AuthUserProfileViewModel
    {
        public Guid UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public List<string> Roles { get; set; } = new();

        public List<string> Permissions { get; set; } = new();

        public Guid TenantId { get; set; }

        public string TenantName { get; set; } = string.Empty;
    }
}