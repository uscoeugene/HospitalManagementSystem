using System;
using System.Collections.Generic;

namespace HMS.API.Application.Billing.DTOs
{
    public class DebtAgingDto
    {
        public int DaysFrom { get; set; }
        public int DaysTo { get; set; }
        public decimal TotalOwed { get; set; }
    }

    public class OutstandingByPatientDto
    {
        public Guid PatientId { get; set; }
        public decimal TotalOwed { get; set; }
        public int DebtCount { get; set; }
    }

    public class PayDebtRequest
    {
        public Guid DebtId { get; set; }
        public decimal Amount { get; set; }
        public string? ExternalReference { get; set; }
    }

    public class PaymentResultDto
    {
        public Guid DebtId { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
        public decimal? AppliedAmount { get; set; }
    }
}
