using System;

namespace HMS.UI.Models.Users
{
    public class RoleViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public IEnumerable<string> Permissions { get; set; } = Array.Empty<string>();
        public bool Selected { get; set; }
    }
}
