using System;

namespace HMS.UI.Models.Payments
{
    public class ReceiptViewModel
    {
        public Guid Id { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public Guid PaymentId { get; set; }
        public DateTimeOffset IssuedAt { get; set; }
        public string Details { get; set; } = string.Empty;
    }
}