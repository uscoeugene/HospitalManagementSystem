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
        public string Currency { get; set; } = "USD";
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
        public string Currency { get; set; } = "USD";
        public int Stock { get; set; }
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
        public string? Category { get; set; }
        public string? Unit { get; set; }
    }
}
