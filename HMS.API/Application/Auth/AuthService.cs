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
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using HMS.API.Application.Common;

namespace HMS.API.Application.Auth
{
    public partial class AuthService : IAuthService
    {
        private readonly AuthDbContext _db;
        private readonly IPasswordHasher _hasher;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthService> _logger;
        private readonly HMS.API.Application.Profile.IProfileService? _profileService;
        private readonly INotificationService? _notificationService;

        public AuthService(AuthDbContext db, IPasswordHasher hasher, IConfiguration config, ILogger<AuthService> logger, HMS.API.Application.Profile.IProfileService? profileService = null, INotificationService? notificationService = null)
        {
            _db = db;
            _hasher = hasher;
            _config = config;
            _logger = logger;
            _profileService = profileService;
            _notificationService = notificationService;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            // Resolve current tenant context (may be null for central)
            var currentTenantId = CurrentTenantAccessor.CurrentTenantId;

            // Lookup user ignoring global query filters (tenant filter) so we can match tenant-specific or central users explicitly
            var userQuery = _db.Users
                .IgnoreQueryFilters()
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
                .Where(u => u.Username == request.Username && !u.IsDeleted && u.TenantId == currentTenantId );

            var user = await userQuery.SingleOrDefaultAsync();

            // If not found in tenant scope, attempt central (TenantId == null) fallback
            if (user is null)
            {
                _logger.LogDebug("User {Username} not found in tenant {TenantId}, attempting central fallback", request.Username, currentTenantId);
                var centralQuery = _db.Users
                    .IgnoreQueryFilters()
                    .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
                    .Where(u => u.Username == request.Username && !u.IsDeleted && u.TenantId == null);

                user = await centralQuery.SingleOrDefaultAsync();
            }

            if (user is null)
            {
                _logger.LogWarning("Login failed for user '{Username}': user not found", request.Username);
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            if (!_hasher.Verify(user.PasswordHash, request.Password))
            {
                _logger.LogWarning("Login failed for user '{Username}': invalid password", request.Username);
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            // If password verified and stored hash is legacy raw SHA-256 (32 bytes base64), rehash to PBKDF2 and persist
            try
            {
                var decoded = Convert.FromBase64String(user.PasswordHash);
                if (decoded.Length == 32)
                {
                    try
                    {
                        var newHash = _hasher.Hash(request.Password);
                        user.PasswordHash = newHash;
                        await _db.SaveChangesAsync();
                        _logger.LogInformation("Migrated legacy password hash for user {Username} to PBKDF2 format", request.Username);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to migrate legacy password hash for user {Username}", request.Username);
                    }
                }
            }
            catch
            {
                // ignore decode errors
            }

            if (user.IsLocked && user.LockedUntil.HasValue && user.LockedUntil > DateTimeOffset.UtcNow)
            {
                _logger.LogWarning("Login failed for user '{Username}': user is locked until {LockedUntil}", request.Username, user.LockedUntil);
                throw new UnauthorizedAccessException("User locked");
            }

            var permissions = user.UserRoles.SelectMany(ur => ur.Role.RolePermissions).Select(rp => rp.Permission.Code).Distinct().ToArray();

            var token = BuildJwtToken(user.Id, user.Username, permissions, user.TenantId);
            var (refreshPlain, refreshEntity) = await CreateRefreshToken(user);

            var audit = new Domain.Auth.AuthAudit
            {
                UserId = user.Id,
                Action = "Login",
                Details = "Successful login"
            };
            _db.AuthAudits.Add(audit);
            await _db.SaveChangesAsync();

            // load tenant info for UI
            TenantDto? tenantDto = null;
            string displayName = user.Username;
            string[] roleNames = user.UserRoles.Select(ur => ur.Role.Name).Distinct().OrderBy(x => x).ToArray();
            if (user.TenantId.HasValue)
            {
                var t = await _db.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == user.TenantId.Value);
                if (t != null)
                {
                    tenantDto = new TenantDto
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Code = t.Code,
                        Address = t.Address,
                        ContactEmail = t.ContactEmail,
                        ContactPhone = t.ContactPhone,
                        LogoUrl = t.LogoUrl
                    };
                }
            }

            try
            {
                if (_profileService != null)
                {
                    var profile = await _profileService.GetByUserIdAsync(user.Id);
                    if (profile != null)
                    {
                        var fullName = string.Join(" ", new[] { profile.FirstName, profile.OtherNames, profile.LastName }
                            .Where(x => !string.IsNullOrWhiteSpace(x)));
                        if (!string.IsNullOrWhiteSpace(fullName))
                        {
                            displayName = fullName;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to build display name for user {UserId}", user.Id);
            }

            return new LoginResponse
            {
                AccessToken = token.tokenString,
                RefreshToken = refreshPlain,
                ExpiresAt = token.expiresAt,
                UserId = user.Id,
                Username = user.Username,
                DisplayName = displayName,
                TenantId = user.TenantId,
                Permissions = permissions,
                Roles = roleNames,
                Tenant = tenantDto
            };
        }

        public async Task<LoginResponse> RegisterAsync(RegisterRequest request)
        {
            if (await _db.Users.AnyAsync(u => u.Username == request.Username)) throw new InvalidOperationException("Username already exists");

            var user = new Domain.Auth.User
            {
                Username = request.Username,
                Email = request.Email,
                PasswordHash = _hasher.Hash(request.Password),
                TenantId = HMS.API.Application.Common.CurrentTenantAccessor.CurrentTenantId
            };

            _db.Users.Add(user);

            // Optionally assign a default role if exists by name "User"
            var defaultRole = await _db.Roles.SingleOrDefaultAsync(r => r.Name == "User");
            if (defaultRole != null)
            {
                _db.UserRoles.Add(new Domain.Auth.UserRole { User = user, Role = defaultRole });
            }

            await _db.SaveChangesAsync();

            // ensure a user profile exists in the HMS DB for this user
            try
            {
                if (_profileService != null)
                {
                    var profileReq = new HMS.API.Application.Profile.DTOs.UpdateUserProfileRequest
                    {
                        FirstName = request.FirstName,
                        LastName = request.LastName,
                        Email = request.Email
                    };

                    await _profileService.CreateOrUpdateAsync(user.Id, profileReq, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create user profile for {UserId}", user.Id);
            }

            // compute permissions from assigned roles
            var perms = await _db.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .Include(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
                .ToListAsync();

            var permissions = perms.SelectMany(ur => ur.Role.RolePermissions).Select(rp => rp.Permission.Code).Distinct().ToArray();
            var roleNames = perms.Select(ur => ur.Role.Name).Distinct().OrderBy(x => x).ToArray();
            var displayName = string.Join(" ", new[] { request.FirstName, request.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = request.Username;
            }

            var token = BuildJwtToken(user.Id, user.Username, permissions, user.TenantId);
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
                Username = user.Username,
                DisplayName = displayName,
                TenantId = user.TenantId,
                Permissions = permissions,
                Roles = roleNames
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

        private (string tokenString, DateTimeOffset expiresAt) BuildJwtToken(Guid userId, string username, IEnumerable<string> permissions, Guid? tenantId = null)
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

            if (tenantId.HasValue)
            {
                claims.Add(new Claim("tenant_id", tenantId.Value.ToString()));
            }

            claims.AddRange(permissions.Select(p => new Claim("permission", p)));

            // Ensure key is at least 256 bits for HS256. If configured key is shorter, derive a 256-bit key using SHA256.
            var keyBytes = Encoding.UTF8.GetBytes(key);
            if (keyBytes.Length < 32)
            {
                using var sha = SHA256.Create();
                keyBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
            }

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
            var token = BuildJwtToken(user.Id, user.Username, permissions, user.TenantId);

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

        public async Task RequestPasswordRecoveryAsync(string email, string resetBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            var tenantId = CurrentTenantAccessor.CurrentTenantId;
            var user = await _db.Users
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(u => !u.IsDeleted && u.Email == email && u.TenantId == tenantId);

            if (user == null && tenantId.HasValue)
            {
                user = await _db.Users
                    .IgnoreQueryFilters()
                    .SingleOrDefaultAsync(u => !u.IsDeleted && u.Email == email && u.TenantId == null);
            }

            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                return;
            }

            var token = BuildPasswordResetToken(user);
            var separator = resetBaseUrl.Contains('?') ? "&" : "?";
            var resetLink = $"{resetBaseUrl}{separator}token={Uri.EscapeDataString(token)}";

            if (_notificationService != null)
            {
                await _notificationService.NotifyAsync("email", new
                {
                    to = user.Email,
                    subject = "HMS password recovery",
                    body = $"""
                        <p>Hello {user.Username},</p>
                        <p>We received a request to reset your HMS password.</p>
                        <p><a href="{resetLink}">Reset your password</a></p>
                        <p>This link expires in 30 minutes.</p>
                        """
                });
            }

            _db.AuthAudits.Add(new Domain.Auth.AuthAudit
            {
                UserId = user.Id,
                Action = "PasswordRecoveryRequested",
                Details = "Password recovery email requested"
            });
            await _db.SaveChangesAsync();
        }

        public async Task<PasswordRecoveryTokenStatusDto> ValidatePasswordResetTokenAsync(string token)
        {
            var principal = ValidatePasswordResetPrincipal(token);
            if (principal == null)
            {
                return new PasswordRecoveryTokenStatusDto { Valid = false };
            }

            var userIdValue = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdValue, out var userId))
            {
                return new PasswordRecoveryTokenStatusDto { Valid = false };
            }

            var passwordStamp = principal.FindFirst("pwd")?.Value ?? string.Empty;
            var user = await _db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
            if (user == null)
            {
                return new PasswordRecoveryTokenStatusDto { Valid = false };
            }

            if (!string.Equals(passwordStamp, ComputeSha256Hash(user.PasswordHash), StringComparison.Ordinal))
            {
                return new PasswordRecoveryTokenStatusDto { Valid = false };
            }

            return new PasswordRecoveryTokenStatusDto
            {
                Valid = true,
                Username = user.Username,
                Email = user.Email
            };
        }

        public async Task ResetPasswordWithTokenAsync(string token, string newPassword)
        {
            var principal = ValidatePasswordResetPrincipal(token);
            if (principal == null)
            {
                throw new InvalidOperationException("Invalid or expired recovery token.");
            }

            var userIdValue = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdValue, out var userId))
            {
                throw new InvalidOperationException("Invalid recovery token.");
            }

            var passwordStamp = principal.FindFirst("pwd")?.Value ?? string.Empty;
            var user = await _db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
            if (user == null)
            {
                throw new InvalidOperationException("User not found.");
            }

            if (!string.Equals(passwordStamp, ComputeSha256Hash(user.PasswordHash), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("This recovery link has already been used or is no longer valid.");
            }

            user.PasswordHash = _hasher.Hash(newPassword);
            user.IsLocked = false;
            user.LockedUntil = null;

            _db.AuthAudits.Add(new Domain.Auth.AuthAudit
            {
                UserId = user.Id,
                Action = "PasswordRecovered",
                Details = "Password reset via email recovery flow"
            });

            await _db.SaveChangesAsync();
        }

        private string BuildPasswordResetToken(Domain.Auth.User user)
        {
            var key = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured");
            var issuer = _config["Jwt:Issuer"] ?? "hms";
            var audience = _config["Jwt:Audience"] ?? "hms_clients";

            var keyBytes = Encoding.UTF8.GetBytes(key);
            if (keyBytes.Length < 32)
            {
                using var sha = SHA256.Create();
                keyBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
            }

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim("typ", "password_reset"),
                new Claim("pwd", ComputeSha256Hash(user.PasswordHash))
            };

            if (user.TenantId.HasValue)
            {
                claims.Add(new Claim("tenant_id", user.TenantId.Value.ToString()));
            }

            var credentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer,
                audience,
                claims,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private ClaimsPrincipal? ValidatePasswordResetPrincipal(string token)
        {
            try
            {
                var key = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured");
                var keyBytes = Encoding.UTF8.GetBytes(key);
                if (keyBytes.Length < 32)
                {
                    using var sha = SHA256.Create();
                    keyBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
                }

                var handler = new JwtSecurityTokenHandler();
                var principal = handler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.FromSeconds(30)
                }, out var validatedToken);

                if (validatedToken is not JwtSecurityToken jwt)
                {
                    return null;
                }

                var tokenType = principal.FindFirst("typ")?.Value;
                return string.Equals(tokenType, "password_reset", StringComparison.Ordinal) ? principal : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
