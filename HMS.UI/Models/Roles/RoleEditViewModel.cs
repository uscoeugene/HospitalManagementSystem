using System;
using System.Collections.Generic;

namespace HMS.UI.Models.Roles
{
    public class RoleEditViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new List<string>();

        // fields for adding permission
        public string NewPermissionCode { get; set; } = string.Empty;
        public string NewPermissionDescription { get; set; } = string.Empty;
    }
}