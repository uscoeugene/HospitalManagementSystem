using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace HMS.API.Application.Auth
{
    public class LocalTokenService
    {
        private readonly IConfiguration _config;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly string _key;

        public LocalTokenService(IConfiguration config)
        {
            _config = config;
            _key = _config["LocalJwt:Key"] ?? "dev-local-key-change";
            _issuer = _config["LocalJwt:Issuer"] ?? "hms_local";
            _audience = _config["LocalJwt:Audience"] ?? "hms_local_clients";
        }

        public (string token, DateTimeOffset expiresAt) BuildLocalJwt(Guid userId, string username, Guid? tenantId, IEnumerable<string> roles, IEnumerable<string> permissions)
        {
            var expires = DateTimeOffset.UtcNow.AddHours(8);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, username),
            };

            if (tenantId.HasValue) claims.Add(new Claim("tenant_id", tenantId.Value.ToString()));
            foreach (var r in roles) claims.Add(new Claim(ClaimTypes.Role, r));
            foreach (var p in permissions) claims.Add(new Claim("permission", p));

            var keyBytes = Encoding.UTF8.GetBytes(_key);
            if (keyBytes.Length < 32)
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                keyBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(_key));
            }

            var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(_issuer, _audience, claims, expires: expires.UtcDateTime, signingCredentials: creds);
            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return (tokenString, expires);
        }
    }
}
