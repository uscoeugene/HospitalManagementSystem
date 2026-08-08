using System;

namespace HMS.API.Application.Pharmacy.DTOs
{
    public class CreateInventoryBatchRequest
    {
        public Guid ItemId { get; set; }
        public Guid StoreId { get; set; }
        public string? BatchNumber { get; set; }
        public DateOnly? ExpiryDate { get; set; }
        public DateOnly? ManufactureDate { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int Quantity { get; set; }
    }
}
