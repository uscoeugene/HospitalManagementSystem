using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Auth
{
    public class LocalPermission : BaseEntity
    {
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid? TenantId { get; set; }
    }
}
