using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Common
{
    public class TenantDomain : BaseEntity
    {
        public Guid TenantId { get; set; }
        public string Domain { get; set; } = string.Empty;
        public bool IsPrimary { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
