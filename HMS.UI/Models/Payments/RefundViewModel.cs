using System;

namespace HMS.UI.Models.Payments
{
    public class RefundViewModel
    {
        public Guid Id { get; set; }
        public Guid PaymentId { get; set; }
        public decimal Amount { get; set; }
        public DateTimeOffset RefundedAt { get; set; }
        public Guid ProcessedBy { get; set; }
        public string Reason { get; set; } = string.Empty;
        public bool IsReversed { get; set; }
    }
}