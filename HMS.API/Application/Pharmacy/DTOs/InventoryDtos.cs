using System;

namespace HMS.API.Application.Pharmacy.DTOs
{
    public class InventoryItemDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal UnitPrice { get; set; }
        public string Currency { get; set; } = "NGN";
        public int Stock { get; set; }
        public int ReservedStock { get; set; }
        public string Category { get; set; } = "general";
        public string? Unit { get; set; }
    }

    public class CreateInventoryItemRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal UnitPrice { get; set; }
        public string Currency { get; set; } = "NGN";
        public int Stock { get; set; }
        public Guid? CategoryId { get; set; }
        public string? Category { get; set; }
        public string? Unit { get; set; }
    }

    public class UpdateInventoryItemRequest
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal? UnitPrice { get; set; }
        public string? Currency { get; set; }
        public Guid? CategoryId { get; set; }
        public string? Category { get; set; }
        public string? Unit { get; set; }
    }

    // Units & conversions
    public class CreateUnitRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class UnitDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class CreateConversionRequest
    {
        public Guid UnitId { get; set; }
        public int BaseQty { get; set; }
    }

    public class ConversionDto
    {
        public Guid Id { get; set; }
        public Guid UnitId { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public int BaseQty { get; set; }
    }

    // Stores
    public class CreateStoreRequest
    {
        public string StoreName { get; set; } = string.Empty;
        public string? StoreType { get; set; }
        public Guid? DepartmentId { get; set; }
    }

    public class StoreDto
    {
        public Guid Id { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string? StoreType { get; set; }
        public Guid? DepartmentId { get; set; }
    }

    // Suppliers
    public class CreateSupplierRequest
    {
        public string SupplierName { get; set; } = string.Empty;
        public string? ContactInfo { get; set; }
    }

    public class SupplierDto
    {
        public Guid Id { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? ContactInfo { get; set; }
    }

    // Receipts
    public class ReceiveGoodsRequest
    {
        public Guid? PurchaseOrderId { get; set; }
        public ReceivedItem[] ReceivedItems { get; set; } = Array.Empty<ReceivedItem>();
    }

    public class ReceivedItem
    {
        public Guid ItemId { get; set; }
        public string? BatchNumber { get; set; }
        public DateOnly? ExpiryDate { get; set; }
        public DateOnly? ManufactureDate { get; set; }
        public int Quantity { get; set; }
        public Guid? UnitId { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
        public Guid StoreId { get; set; }
    }

    public class BatchDto
    {
        public Guid Id { get; set; }
        public Guid ItemId { get; set; }
        public Guid StoreId { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public DateOnly? ExpiryDate { get; set; }
        public DateOnly? ManufactureDate { get; set; }
        public int ReceivedQty { get; set; }
        public int AvailableQty { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
    }

    public class StockLookupDto
    {
        public Guid? StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }
}
