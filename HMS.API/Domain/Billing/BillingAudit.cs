using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Billing
{
    public class BillingAudit : BaseEntity
    {
        public Guid UserId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTimeOffset PerformedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}