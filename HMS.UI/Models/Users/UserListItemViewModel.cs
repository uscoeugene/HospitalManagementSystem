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
    }
}
