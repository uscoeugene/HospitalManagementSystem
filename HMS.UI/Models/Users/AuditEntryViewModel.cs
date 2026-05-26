using System;

namespace HMS.UI.Models.Users
{
    public class AuditEntryViewModel
    {
        public DateTimeOffset PerformedAt { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? Details { get; set; }
    }
}
