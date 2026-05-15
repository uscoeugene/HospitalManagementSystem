using System;
using System.Threading.Tasks;

namespace HMS.API.Application.Common
{
    public interface ITenantResolver
    {
        Task<Guid?> ResolveTenantIdAsync();
        Task<Guid?> ResolveTenantIdFromHostAsync(string host);
    }
}
