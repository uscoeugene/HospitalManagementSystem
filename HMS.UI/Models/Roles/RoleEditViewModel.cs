using System;
using System.Collections.Generic;

namespace HMS.UI.Models.Roles
{
    public class RoleEditViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSystem { get; set; }
        public List<string> Permissions { get; set; } = new List<string>();
        public List<PermissionOptionViewModel> AvailablePermissions { get; set; } = new List<PermissionOptionViewModel>();
        public bool CanManagePermissionCatalog { get; set; }
    }
}
