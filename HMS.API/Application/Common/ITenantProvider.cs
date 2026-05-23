using System;
using System.Threading.Tasks;
using HMS.API.Application.Auth.DTOs;

namespace HMS.API.Application.Common
{
    public interface ITenantProvider
    {
        Task<Guid?> GetCurrentTenantIdAsync();
        Task<TenantDto?> GetCurrentTenantAsync();
    }
}
