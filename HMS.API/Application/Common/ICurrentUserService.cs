using System;

namespace HMS.API.Application.Common
{
    public interface ICurrentUserService
    {
        Guid? UserId { get; }
        bool HasPermission(string permission);
    }
}