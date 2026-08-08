using System;
using System.Collections.Generic;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Pharmacy
{
    public class Service : BaseEntity
    {
        public string ServiceCode { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public Guid? DepartmentId { get; set; }
        public decimal Price { get; set; }

        public ICollection<ServiceItem> Items { get; set; } = new List<ServiceItem>();
    }
}
