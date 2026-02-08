using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Common
{
    public class TenantNode : BaseEntity
    {
        public Guid TenantId { get; set; }
        public string CallbackUrl { get; set; } = string.Empty;
        public string? Name { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;
        // Base64-encoded secret shared between central and this node for push verification
        public string? CallbackSecret { get; set; }
    }
}
