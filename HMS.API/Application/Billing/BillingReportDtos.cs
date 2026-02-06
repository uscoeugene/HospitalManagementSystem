using System;

namespace HMS.API.Application.Billing
{
    public class BillingSummaryKpiDto
    {
        public decimal TotalRevenue { get; set; }
        public int InvoiceCount { get; set; }
        public int PaidCount { get; set; }
        public int UnpaidCount { get; set; }
        public decimal AverageInvoice { get; set; }
    }

    public class MonthlyRevenueDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Revenue { get; set; }
    }

    public class StatusBreakdownDto
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class DailyRevenueDto
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public int Invoices { get; set; }
    }

    public class TopPatientDto
    {
        public Guid PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public decimal TotalPaid { get; set; }
        public int PaymentsCount { get; set; }
    }

    public class RefundReportDto
    {
        public Guid RefundId { get; set; }
        public Guid PaymentId { get; set; }
        public Guid InvoiceId { get; set; }
        public decimal Amount { get; set; }
        public DateTimeOffset RefundedAt { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
