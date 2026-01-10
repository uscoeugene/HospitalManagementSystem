using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Billing
{
    public class InvoiceItem : BaseEntity
    {
        public Guid InvoiceId { get; set; }
        public Invoice Invoice { get; set; } = null!;

        public string Description { get; set; } = string.Empty;

        public decimal UnitPrice { get; set; }

        public int Quantity { get; set; }

        public decimal LineTotal => UnitPrice * Quantity;

        public Guid? SourceId { get; set; } // e.g., Lab order / Pharmacy dispense reference
        public string? SourceType { get; set; }
    }
}