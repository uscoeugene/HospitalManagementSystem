using System;
using System.Collections.Generic;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Billing
{
    public enum InvoiceStatus
    {
        UNPAID,
        PARTIAL,
        PAID,
        CANCELLED
    }

    public class Invoice : BaseEntity
    {
        public Guid PatientId { get; set; }

        public Guid? VisitId { get; set; }

        public string InvoiceNumber { get; set; } = string.Empty;

        public InvoiceStatus Status { get; set; } = InvoiceStatus.UNPAID;

        public decimal TotalAmount { get; set; }

        public decimal AmountPaid { get; set; }

        public string Currency { get; set; } = "USD";

        public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();

        public ICollection<InvoicePayment> Payments { get; set; } = new List<InvoicePayment>();
    }
}