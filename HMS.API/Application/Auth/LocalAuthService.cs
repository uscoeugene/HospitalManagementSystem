using System;
using System.Threading.Tasks;
using HMS.API.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using HMS.API.Application.Auth.DTOs;
using HMS.API.Domain.Auth;
using HMS.API.Application.Common;

namespace HMS.API.Application.Auth
{
    // Service to manage local users for offline login and sync
    public class LocalAuthService
    {
        private readonly AuthDbContext _authDb;
        private readonly IPasswordHasher _hasher;

        public LocalAuthService(AuthDbContext authDb, IPasswordHasher hasher)
        {
            _authDb = authDb;
            _hasher = hasher;
        }

        public async Task<LoginResponse> LoginLocalAsync(LoginRequest request)
        {
            var user = await _authDb.Set<LocalUser>().SingleOrDefaultAsync(u => u.Username == request.Username && !u.IsDeleted);
            if (user == null) throw new UnauthorizedAccessException("Invalid credentials");
            if (!_hasher.Verify(user.PasswordHash, request.Password)) throw new UnauthorizedAccessException("Invalid credentials");

            // return a login response without tokens (local-only) - client can work offline with limited features
            return new LoginResponse { UserId = user.Id, TenantId = user.TenantId, Permissions = Array.Empty<string>() };
        }

        public async Task RegisterLocalAsync(RegisterRequest req, Guid? tenantId = null)
        {
            if (await _authDb.Set<LocalUser>().AnyAsync(u => u.Username == req.Username && !u.IsDeleted)) throw new InvalidOperationException("Username already exists locally");
            var lu = new LocalUser { Username = req.Username, PasswordHash = _hasher.Hash(req.Password), Email = req.Email, TenantId = tenantId };
            _authDb.Set<LocalUser>().Add(lu);
            await _authDb.SaveChangesAsync();
        }
    }
}
