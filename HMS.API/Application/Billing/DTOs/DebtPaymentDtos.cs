using System;

namespace HMS.API.Application.Billing.DTOs
{
    public class BatchPayDebtRequest
    {
        public Guid DebtId { get; set; }
        public decimal Amount { get; set; }
        public string? ExternalReference { get; set; }
    }

    public class DebtPaymentResultDto
    {
        public Guid DebtId { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
        public decimal? AppliedAmount { get; set; }
    }
}
