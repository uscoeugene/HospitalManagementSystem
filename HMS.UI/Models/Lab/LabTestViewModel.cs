using System;
using System.ComponentModel.DataAnnotations;

namespace HMS.UI.Models.Lab
{
    public class LabTestViewModel
    {
        public Guid Id { get; set; }

        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        public decimal Price { get; set; }

        [MaxLength(10)]
        public string Currency { get; set; } = "NGN";
    }
}
