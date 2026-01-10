using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Common
{
    public class OutboxMessage : BaseEntity
    {
        public string Type { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? ProcessedAt { get; set; }
        public int Attempts { get; set; } = 0;
    }
}