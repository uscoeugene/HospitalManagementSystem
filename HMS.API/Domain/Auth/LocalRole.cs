using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Auth
{
    public class LocalRole : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid? TenantId { get; set; }
    }
}
