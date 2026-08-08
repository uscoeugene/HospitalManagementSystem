using System;
using System.Collections.Generic;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Pharmacy
{
    public enum PurchaseOrderStatus
    {
        DRAFT,
        SUBMITTED,
        RECEIVED,
        CANCELLED
    }

    public class PurchaseOrder : BaseEntity
    {
        public Guid SupplierId { get; set; }
        public Supplier Supplier { get; set; } = null!;

        public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.DRAFT;
        public DateTimeOffset OrderDate { get; set; } = DateTimeOffset.UtcNow;

        public ICollection<PurchaseOrderLine> Items { get; set; } = new List<PurchaseOrderLine>();
    }
}
