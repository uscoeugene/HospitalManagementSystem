using System;

namespace HMS.API.Application.Common
{
    public interface ICurrentUserService
    {
        Guid? UserId { get; }
        Guid? TenantId { get; }
        System.Collections.Generic.IEnumerable<Guid> DepartmentIds { get; }
        bool HasPermission(string permission);
    }
}