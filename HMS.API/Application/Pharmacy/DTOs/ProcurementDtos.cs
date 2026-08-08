using System;
using System.Collections.Generic;

namespace HMS.API.Application.Pharmacy.DTOs
{
    public class CreatePurchaseOrderRequest
    {
        public Guid SupplierId { get; set; }
        public DateTimeOffset? OrderDate { get; set; }
        public List<CreatePurchaseOrderLineRequest> Items { get; set; } = new();
    }

    public class CreatePurchaseOrderLineRequest
    {
        public Guid ItemId { get; set; }
        public int Quantity { get; set; }
        public Guid? UnitId { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class ReceivePurchaseOrderRequest
    {
        // for each PO line, provide batch info
        public List<ReceivePurchaseOrderLineRequest> ReceivedLines { get; set; } = new();
    }

    public class ReceivePurchaseOrderLineRequest
    {
        public Guid PurchaseOrderLineId { get; set; }
        public Guid StoreId { get; set; }
        public string? BatchNumber { get; set; }
        public DateOnly? ExpiryDate { get; set; }
        public DateOnly? ManufactureDate { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int Quantity { get; set; }
    }
}
