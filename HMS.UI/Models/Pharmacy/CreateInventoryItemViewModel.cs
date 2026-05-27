using System;
using System.ComponentModel.DataAnnotations;

namespace HMS.UI.Models.Pharmacy
{
    public class CreateInventoryItemViewModel
    {
        [Required]
        public string Code { get; set; } = string.Empty;
        [Required]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal UnitPrice { get; set; }
        public string Currency { get; set; } = "NGN";
        public int Stock { get; set; }
        public string? Category { get; set; }
        public string? Unit { get; set; }
    }
}
