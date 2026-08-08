using System;
using HMS.API.Domain.Common;
using HMS.API.Domain.Organization;

namespace HMS.API.Domain.Pharmacy
{
    public class Store : BaseEntity
    {
        public string StoreName { get; set; } = string.Empty;
        public string? StoreType { get; set; }
        // Associate store with a department so inventory can be scoped by department
        public Guid? DepartmentId { get; set; }
        public Department? Department { get; set; }
    }
}
