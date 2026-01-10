using System;

namespace HMS.API.Application.Billing
{
    public class InvoiceStatusChangedEvent
    {
        public Guid InvoiceId { get; set; }
        public string NewStatus { get; set; } = string.Empty;
    }
}