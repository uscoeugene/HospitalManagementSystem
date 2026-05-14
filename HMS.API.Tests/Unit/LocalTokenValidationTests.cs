using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using HMS.API.Application.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace HMS.API.Tests.Unit
{
    public class LocalTokenValidationTests
    {
        [Fact]
        public void LocalToken_CanBeSignedWith_LocalKey_AndValidated_With_KeyBytesDerived()
        {
            var inMemory = new System.Collections.Generic.Dictionary<string, string?>
            {
                ["LocalJwt:Key"] = "dev-local-key-change",
                ["LocalJwt:Issuer"] = "hms_local",
                ["LocalJwt:Audience"] = "hms_local_clients"
            };

            var cfg = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
            var svc = new LocalTokenService(cfg);

            var (token, expires) = svc.BuildLocalJwt(System.Guid.NewGuid(), "tester", null, new string[0], new string[0]);

            // Validate using the same key derivation as Program.cs
            var key = cfg["LocalJwt:Key"]!;
            var keyBytes = Encoding.UTF8.GetBytes(key);
            if (keyBytes.Length < 32)
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                keyBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
            }

            var handler = new JwtSecurityTokenHandler();
            var tvp = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                ValidateIssuer = true,
                ValidIssuer = cfg["LocalJwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = cfg["LocalJwt:Audience"],
                ClockSkew = System.TimeSpan.FromSeconds(30)
            };

            var principal = handler.ValidateToken(token, tvp, out var validatedToken);
            principal.Claims.Any(c => c.Type == ClaimTypes.Name || c.Type == ClaimTypes.NameIdentifier || c.Type == JwtRegisteredClaimNames.Sub).Should().BeTrue();
        }
    }
}
