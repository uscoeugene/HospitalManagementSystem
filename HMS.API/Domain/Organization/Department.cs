using System;
using System.Collections.Generic;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Organization
{
    public class Department : BaseEntity
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        // optional parent for hierarchical departments
        public Guid? ParentDepartmentId { get; set; }
        public Department? ParentDepartment { get; set; }

        public ICollection<HMS.API.Domain.Pharmacy.Store>? Stores { get; set; }
    }
}
