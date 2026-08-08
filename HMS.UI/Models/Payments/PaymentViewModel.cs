using System;

namespace HMS.UI.Models.Payments
{
    public class PaymentViewModel
    {
        public Guid Id { get; set; }
        public Guid InvoiceId { get; set; }
        public Guid PatientId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "NGN";
        public string Status { get; set; } = string.Empty;
        public string? ExternalReference { get; set; }
    }
}