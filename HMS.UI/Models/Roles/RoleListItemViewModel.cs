using System;
using System.Collections.Generic;

namespace HMS.UI.Models.Roles
{
    public class RoleListItemViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public IEnumerable<string> Permissions { get; set; } = Array.Empty<string>();
    }
}