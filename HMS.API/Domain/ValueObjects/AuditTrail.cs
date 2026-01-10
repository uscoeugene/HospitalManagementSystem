using System;
using System.Collections.Generic;

namespace HMS.API.Domain.ValueObjects
{
    public sealed class AuditTrail
    {
        public Guid EntityId { get; }

        public List<AuditEntry> Entries { get; } = new List<AuditEntry>();

        public AuditTrail(Guid entityId)
        {
            EntityId = entityId;
        }

        public void AddEntry(Guid performedBy, string action, string? details = null)
        {
            Entries.Add(new AuditEntry
            {
                PerformedBy = performedBy,
                Action = action,
                Details = details,
                PerformedAt = DateTimeOffset.UtcNow
            });
        }
    }

    public sealed class AuditEntry
    {
        public Guid PerformedBy { get; set; }

        public string Action { get; set; } = string.Empty;

        public string? Details { get; set; }

        public DateTimeOffset PerformedAt { get; set; }
    }
}