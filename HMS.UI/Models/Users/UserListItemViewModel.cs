using System;

namespace HMS.UI.Models.Users
{
    public class UserListItemViewModel
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public Guid? TenantId { get; set; }
        public bool IsLocked { get; set; }
        public string? PhotoUrl { get; set; }
        public DateTimeOffset? LastLogin { get; set; }
        // UI-friendly fields
        public string? TenantName { get; set; }
        public string? FullName { get; set; }
        public string[] Roles { get; set; } = Array.Empty<string>();
        public HMS.UI.Models.Users.AuditEntryViewModel[] Activity { get; set; } = Array.Empty<HMS.UI.Models.Users.AuditEntryViewModel>();
    }
}
