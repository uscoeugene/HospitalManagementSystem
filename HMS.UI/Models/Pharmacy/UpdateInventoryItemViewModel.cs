using System;
using System.ComponentModel.DataAnnotations;

namespace HMS.UI.Models.Pharmacy
{
    public class UpdateInventoryItemViewModel
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
