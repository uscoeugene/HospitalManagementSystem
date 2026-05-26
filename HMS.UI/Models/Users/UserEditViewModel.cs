using System;

namespace HMS.UI.Models.Users
{
    public class UserEditViewModel
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhotoUrl { get; set; }
        public Guid[] AssignedRoleIds { get; set; } = Array.Empty<Guid>();
        public RoleViewModel[] Roles { get; set; } = Array.Empty<RoleViewModel>();
        public string? TenantName { get; set; }
    }
}
