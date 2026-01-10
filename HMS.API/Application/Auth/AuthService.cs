using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HMS.API.Application.Auth.DTOs;
using HMS.API.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace HMS.API.Application.Auth
{
    public partial class AuthService : IAuthService
    {
        private readonly AuthDbContext _db;
        private readonly IPasswordHasher _hasher;
        private readonly IConfiguration _config;

        public AuthService(AuthDbContext db, IPasswordHasher hasher, IConfiguration config)
        {
            _db = db;
            _hasher = hasher;
            _config = config;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var user = await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
                .SingleOrDefaultAsync(u => u.Username == request.Username && !u.IsDeleted);

            if (user is null) throw new UnauthorizedAccessException("Invalid credentials");
            if (!_hasher.Verify(user.PasswordHash, request.Password)) throw new UnauthorizedAccessException("Invalid credentials");
            if (user.IsLocked && user.LockedUntil.HasValue && user.LockedUntil > DateTimeOffset.UtcNow) throw new UnauthorizedAccessException("User locked");

            var permissions = user.UserRoles.SelectMany(ur => ur.Role.RolePermissions).Select(rp => rp.Permission.Code).Distinct().ToArray();

            var token = BuildJwtToken(user.Id, user.Username, permissions);
            var (refreshPlain, refreshEntity) = await CreateRefreshToken(user);

            var audit = new Domain.Auth.AuthAudit
            {
                UserId = user.Id,
                Action = "Login",
                Details = "Successful login"
            };
            _db.AuthAudits.Add(audit);
            await _db.SaveChangesAsync();

            return new LoginResponse
            {
                AccessToken = token.tokenString,
                RefreshToken = refreshPlain,
                ExpiresAt = token.expiresAt,
                UserId = user.Id,
                Permissions = permissions
            };
        }

        public async Task<LoginResponse> RegisterAsync(RegisterRequest request)
        {
            if (await _db.Users.AnyAsync(u => u.Username == request.Username)) throw new InvalidOperationException("Username already exists");

            var user = new Domain.Auth.User
            {
                Username = request.Username,
                Email = request.Email,
                PasswordHash = _hasher.Hash(request.Password)
            };

            _db.Users.Add(user);

            // Optionally assign a default role if exists by name "User"
            var defaultRole = await _db.Roles.SingleOrDefaultAsync(r => r.Name == "User");
            if (defaultRole != null)
            {
                _db.UserRoles.Add(new Domain.Auth.UserRole { User = user, Role = defaultRole });
            }

            await _db.SaveChangesAsync();

            // issue token
            var permissions = new string[0];
            var token = BuildJwtToken(user.Id, user.Username, permissions);
            var (refreshPlain, refreshEntity) = await CreateRefreshToken(user);

            var audit = new Domain.Auth.AuthAudit
            {
                UserId = user.Id,
                Action = "Register",
                Details = "User registered"
            };
            _db.AuthAudits.Add(audit);
            await _db.SaveChangesAsync();

            return new LoginResponse
            {
                AccessToken = token.tokenString,
                RefreshToken = refreshPlain,
                ExpiresAt = token.expiresAt,
                UserId = user.Id,
                Permissions = permissions
            };
        }

        private async Task<(string Plain, Domain.Auth.RefreshToken Entity)> CreateRefreshToken(Domain.Auth.User user)
        {
            var plain = GenerateSecureToken(64);
            var hash = ComputeSha256Hash(plain);

            var rt = new Domain.Auth.RefreshToken
            {
                User = user,
                TokenHash = hash,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
            };

            _db.RefreshTokens.Add(rt);
            await _db.SaveChangesAsync();
            return (plain, rt);
        }

        private (string tokenString, DateTimeOffset expiresAt) BuildJwtToken(Guid userId, string username, IEnumerable<string> permissions)
        {
            var key = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured");
            var issuer = _config["Jwt:Issuer"] ?? "hms";
            var audience = _config["Jwt:Audience"] ?? "hms_clients";

            var expires = DateTimeOffset.UtcNow.AddHours(8);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, username),
            };

            claims.AddRange(permissions.Select(p => new Claim("permission", p)));

            var keyBytes = Encoding.UTF8.GetBytes(key);
            var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(issuer, audience, claims, expires: expires.UtcDateTime, signingCredentials: creds);
            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return (tokenString, expires);
        }

        private static string ComputeSha256Hash(string raw)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(raw);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private static string GenerateSecureToken(int size = 64)
        {
            var bytes = new byte[size];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        public async Task<RefreshResponse> RefreshAsync(RefreshRequest request)
        {
            var hash = ComputeSha256Hash(request.RefreshToken);
            var rt = await _db.RefreshTokens.Include(r => r.User).SingleOrDefaultAsync(r => r.TokenHash == hash && !r.IsRevoked && r.ExpiresAt > DateTimeOffset.UtcNow);
            if (rt == null) throw new InvalidOperationException("Invalid refresh token");

            var user = rt.User;
            var permissions = user.UserRoles.SelectMany(ur => ur.Role.RolePermissions).Select(rp => rp.Permission.Code).Distinct().ToArray();
            var token = BuildJwtToken(user.Id, user.Username, permissions);

            // rotate refresh token
            rt.IsRevoked = true;
            rt.RevokedAt = DateTimeOffset.UtcNow;

            var (newPlain, newRt) = await CreateRefreshToken(user);

            await _db.SaveChangesAsync();

            return new RefreshResponse
            {
                AccessToken = token.tokenString,
                RefreshToken = newPlain,
                ExpiresAt = token.expiresAt
            };
        }

        public async Task RevokeRefreshAsync(string refreshToken)
        {
            var hash = ComputeSha256Hash(refreshToken);
            var rt = await _db.RefreshTokens.SingleOrDefaultAsync(r => r.TokenHash == hash && !r.IsRevoked);
            if (rt == null) return;
            rt.IsRevoked = true;
            rt.RevokedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}