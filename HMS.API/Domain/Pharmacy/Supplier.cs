using System;
using System.Collections.Generic;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Pharmacy
{
    public class Supplier : BaseEntity
    {
        public string SupplierName { get; set; } = string.Empty;
        public string? ContactInfo { get; set; }

        public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
    }
}
