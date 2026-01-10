using System;
using System.Collections.Generic;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Auth
{
    public class Permission : BaseEntity
    {
        // Atomic permission code used in policies
        public string Code { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}