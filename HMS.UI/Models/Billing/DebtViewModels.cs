using System;

namespace HMS.UI.Models.Billing
{
    public class DebtViewModel
    {
        public Guid Id { get; set; }
        public Guid InvoiceId { get; set; }
        public Guid? SourceItemId { get; set; }
        public string? SourceType { get; set; }
        public decimal AmountOwed { get; set; }
        public string? Reason { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public Guid CreatedBy { get; set; }
    }

    public class DebtAgingViewModel
    {
        public int DaysFrom { get; set; }
        public int DaysTo { get; set; }
        public decimal TotalOwed { get; set; }
    }

    public class OutstandingByPatientViewModel
    {
        public Guid PatientId { get; set; }
        public decimal TotalOwed { get; set; }
        public int DebtCount { get; set; }
    }

    public class BatchPayDebtRequest
    {
        public Guid DebtId { get; set; }
        public decimal Amount { get; set; }
        public string? ExternalReference { get; set; }
    }
}