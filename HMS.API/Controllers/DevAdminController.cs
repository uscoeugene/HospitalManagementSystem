using System;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using HMS.API.Application.Auth;
using HMS.API.Infrastructure.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Security.Cryptography;
using System.Collections.Generic;
using HMS.API.Infrastructure.Persistence;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("dev/admin")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class DevAdminController : ControllerBase
    {
        private readonly AuthDbContext _db;
        private readonly HmsDbContext _hdb;
        private readonly IPasswordHasher _hasher;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        public DevAdminController(AuthDbContext db, HmsDbContext hdb, IPasswordHasher hasher, IWebHostEnvironment env, IConfiguration config)
        {
            _db = db;
            _hdb = hdb;
            _hasher = hasher;
            _env = env;
            _config = config;
        }

        public class ResetPasswordRequest
        {
            public string Username { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
        {
            // Only allow in development to avoid accidental exposure
            if (!_env.IsDevelopment()) return Forbid();

            // Bypass global query filters so tenant-specific users can be found
            var user = await _db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.Username == req.Username && !u.IsDeleted);
            if (user == null) return NotFound(new { error = "User not found" });

            user.PasswordHash = _hasher.Hash(req.NewPassword);
            user.IsLocked = false;
            user.LockedUntil = null;

            await _db.SaveChangesAsync();

            // immediate verify using the stored hash
            bool immediateVerify = false;
            try
            {
                immediateVerify = _hasher.Verify(user.PasswordHash, req.NewPassword);
            }
            catch { immediateVerify = false; }

            return Ok(new { message = "Password reset", storedPasswordHash = user.PasswordHash, immediateVerify });
        }

        public class CheckLoginRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        [HttpPost("check-login")]
        public async Task<IActionResult> CheckLogin([FromBody] CheckLoginRequest req)
        {
            if (!_env.IsDevelopment()) return Forbid();

            // Bypass global query filters for diagnostics
            var user = await _db.Users.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(u => u.Username == req.Username && !u.IsDeleted);
            if (user == null)
            {
                var local = await _db.Set<HMS.API.Domain.Auth.LocalUser>().IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(u => u.Username == req.Username && !u.IsDeleted);
                return Ok(new { existsInAuth = false, existsInLocal = local != null });
            }

            var verify = false;
            try
            {
                verify = _hasher.Verify(user.PasswordHash, req.Password);
            }
            catch
            {
                verify = false;
            }

            // decode stored hash for diagnostics
            object? decodedInfo = null;
            try
            {
                var bytes = Convert.FromBase64String(user.PasswordHash);
                decodedInfo = new
                {
                    TotalBytes = bytes.Length,
                    FormatMarker = bytes.Length > 0 ? bytes[0] : (int?)null,
                    SaltLength = bytes.Length > 1 ? 16 : (int?)null,
                    SubkeyLength = bytes.Length > 17 ? bytes.Length - 17 : (int?)null
                };
            }
            catch (Exception ex)
            {
                decodedInfo = new { error = "invalid-base64", message = ex.Message };
            }

            return Ok(new
            {
                existsInAuth = true,
                user = new { user.Username, user.Email, user.IsLocked, user.LockedUntil, user.TenantId },
                storedPasswordHash = user.PasswordHash,
                passwordVerify = verify,
                decoded = decodedInfo
            });
        }

        [HttpPost("verify-hash")]
        public async Task<IActionResult> VerifyHash([FromBody] CheckLoginRequest req)
        {
            if (!_env.IsDevelopment()) return Forbid();

            var user = await _db.Users.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(u => u.Username == req.Username && !u.IsDeleted);
            if (user == null) return NotFound(new { error = "User not found" });

            byte[] decoded;
            try
            {
                decoded = Convert.FromBase64String(user.PasswordHash);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "stored-hash-not-base64", message = ex.Message });
            }

            if (!(decoded.Length == 1 + 16 + 32 && decoded[0] == 0x01))
            {
                return BadRequest(new { error = "unsupported-hash-format", length = decoded.Length });
            }

            var salt = new byte[16];
            Buffer.BlockCopy(decoded, 1, salt, 0, 16);
            var expectedSubkey = new byte[32];
            Buffer.BlockCopy(decoded, 1 + 16, expectedSubkey, 0, 32);

            var actualSubkey = KeyDerivation.Pbkdf2(req.Password, salt, KeyDerivationPrf.HMACSHA256, 100_000, 32);

            var match = CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);

            return Ok(new
            {
                username = req.Username,
                match,
                expectedSubkey = Convert.ToBase64String(expectedSubkey),
                computedSubkey = Convert.ToBase64String(actualSubkey),
                salt = Convert.ToBase64String(salt)
            });
        }

        [HttpGet("info")]
        public IActionResult Info()
        {
            if (!_env.IsDevelopment()) return Forbid();

            var conn = _db.Database.GetDbConnection()?.ConnectionString ?? "";
            return Ok(new { connection = conn, hasher = _hasher.GetType().FullName });
        }

        public class IssueTokenRequest { public string Username { get; set; } = string.Empty; }

        [HttpPost("issue-token")]
        public async Task<IActionResult> IssueToken([FromBody] IssueTokenRequest req)
        {
            if (!_env.IsDevelopment()) return Forbid();

            var user = await _db.Users.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(u => u.Username == req.Username && !u.IsDeleted);
            if (user == null) return NotFound(new { error = "User not found" });

            var jwtKey = _config["Jwt:Key"] ?? "dev-insecure-key-change";
            var issuer = _config["Jwt:Issuer"] ?? "hms";
            var audience = _config["Jwt:Audience"] ?? "hms_clients";

            var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
            if (keyBytes.Length < 32)
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                keyBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(jwtKey));
            }
            var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

            var claims = new[] {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username)
            };

            var expires = DateTimeOffset.UtcNow.AddHours(8);
            var token = new JwtSecurityToken(issuer, audience, claims, expires: expires.UtcDateTime, signingCredentials: creds);
            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            var cookieOptions = new Microsoft.AspNetCore.Http.CookieOptions { HttpOnly = true, Secure = true, SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict, Expires = expires.UtcDateTime };
            Response.Cookies.Append("HmsAuth", tokenString, cookieOptions);

            return Ok(new { message = "Token issued and cookie set", expiresAt = expires });
        }

        [HttpPost("reseed")]
        public async Task<IActionResult> Reseed()
        {
            if (!_env.IsDevelopment()) return Forbid();

            // Delete dependent entities in correct order
            // Use IgnoreQueryFilters to ensure tenant-scoped rows are included
            var rolePermissions = await _db.RolePermissions.IgnoreQueryFilters().ToListAsync();
            _db.RolePermissions.RemoveRange(rolePermissions);

            var userRoles = await _db.UserRoles.IgnoreQueryFilters().ToListAsync();
            _db.UserRoles.RemoveRange(userRoles);

            var refreshTokens = await _db.RefreshTokens.IgnoreQueryFilters().ToListAsync();
            _db.RefreshTokens.RemoveRange(refreshTokens);

            var users = await _db.Users.IgnoreQueryFilters().ToListAsync();
            _db.Users.RemoveRange(users);

            var roles = await _db.Roles.IgnoreQueryFilters().ToListAsync();
            _db.Roles.RemoveRange(roles);

            var permissions = await _db.Permissions.IgnoreQueryFilters().ToListAsync();
            _db.Permissions.RemoveRange(permissions);

            // Tenants and related subscription/node data
            var tenantSubs = await _db.Set<HMS.API.Domain.Common.TenantSubscription>().IgnoreQueryFilters().ToListAsync();
            _db.Set<HMS.API.Domain.Common.TenantSubscription>().RemoveRange(tenantSubs);

            var tenantNodes = await _db.Set<HMS.API.Domain.Common.TenantNode>().IgnoreQueryFilters().ToListAsync();
            _db.Set<HMS.API.Domain.Common.TenantNode>().RemoveRange(tenantNodes);

            var tenants = await _db.Tenants.IgnoreQueryFilters().ToListAsync();
            _db.Tenants.RemoveRange(tenants);

            var localUsers = await _db.Set<HMS.API.Domain.Auth.LocalUser>().IgnoreQueryFilters().ToListAsync();
            _db.Set<HMS.API.Domain.Auth.LocalUser>().RemoveRange(localUsers);

            await _db.SaveChangesAsync();

            // Re-run seed
            await HMS.API.Infrastructure.Auth.SeedData.EnsureSeedDataAsync(_db, _hasher);

            return Ok(new { message = "Reseeded auth DB" });
        }

        [HttpPost("wipe-all")]
        [HttpPost("/admin/wipe-all")]
        public async Task<IActionResult> WipeAllAndReseed()
        {
           // if (!_env.IsDevelopment()) return Forbid();

            try
            {
                // Drop both databases (auth and hms share the same connection in current config)
                await _db.Database.EnsureDeletedAsync();
                await _hdb.Database.EnsureDeletedAsync();

                // Recreate via migrations
                await _db.Database.MigrateAsync();
                await _hdb.Database.MigrateAsync();

                // Reseed
                await HMS.API.Infrastructure.Auth.SeedData.EnsureSeedDataAsync(_db, _hasher);
                await HMS.API.Infrastructure.Persistence.HmsSeedData.EnsureSeedDataAsync(_hdb, _db, _hasher);

                return Ok(new { message = "Wiped databases and reseeded" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
