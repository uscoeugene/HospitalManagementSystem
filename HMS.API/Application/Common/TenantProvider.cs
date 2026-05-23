using System;
using System.Threading.Tasks;
using HMS.API.Application.Auth.DTOs;
using HMS.API.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;

namespace HMS.API.Application.Common
{
    public class TenantProvider : ITenantProvider
    {
        private readonly AuthDbContext _authDb;

        public TenantProvider(AuthDbContext authDb)
        {
            _authDb = authDb;
        }

        public async Task<Guid?> GetCurrentTenantIdAsync()
        {
            return CurrentTenantAccessor.CurrentTenantId;
        }

        public async Task<TenantDto?> GetCurrentTenantAsync()
        {
            var tid = CurrentTenantAccessor.CurrentTenantId;
            if (!tid.HasValue) return null;
            var t = await _authDb.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == tid.Value);
            if (t == null) return null;
            return new TenantDto { Id = t.Id, Name = t.Name, Code = t.Code, Address = t.Address, ContactEmail = t.ContactEmail, ContactPhone = t.ContactPhone, LogoUrl = t.LogoUrl };
        }
    }
}
