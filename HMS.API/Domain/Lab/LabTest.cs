using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Lab
{
    public class LabTest : BaseEntity
    {
        public string Code { get; set; } = string.Empty; // e.g., LFT, CBC
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Currency { get; set; } = "USD";
    }
}