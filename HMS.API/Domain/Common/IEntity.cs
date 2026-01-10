using System;

namespace HMS.API.Domain.Common
{
    public interface IEntity
    {
        Guid Id { get; set; }
    }
}