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

        public LocalTokenService(IConfiguration config)
        {
            _config = config;
        }

        public (string token, DateTimeOffset expiresAt) BuildLocalJwt(Guid userId, string username, Guid? tenantId, IEnumerable<string> roles, IEnumerable<string> permissions)
        {
            var key = _config["LocalJwt:Key"] ?? throw new InvalidOperationException("LocalJwt:Key not configured");
            var issuer = _config["LocalJwt:Issuer"] ?? "hms_local";
            var audience = _config["LocalJwt:Audience"] ?? "hms_local_clients";

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

            var keyBytes = Encoding.UTF8.GetBytes(key);
            var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(issuer, audience, claims, expires: expires.UtcDateTime, signingCredentials: creds);
            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return (tokenString, expires);
        }
    }
}
