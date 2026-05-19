using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace HMS.UI.Services
{
    // Provides a stable additional data string for antiforgery tokens for authenticated users
    // Uses NameIdentifier/sub/name claim if present, otherwise falls back to a hash of all claims
    public class AntiforgeryAdditionalDataProvider : IAntiforgeryAdditionalDataProvider
    {
        public string? GetAdditionalData(HttpContext context)
        {
            var user = context?.User;
            if (user == null) return null;

            if (user.Identity?.IsAuthenticated != true) return null;

            // Prefer stable id claims
            var id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? user.FindFirst("sub")?.Value
                     ?? user.FindFirst(ClaimTypes.Name)?.Value;

            if (!string.IsNullOrWhiteSpace(id)) return id;

            // fallback: compute deterministic hash of claims
            try
            {
                var sb = new StringBuilder();
                foreach (var c in user.Claims.OrderBy(c => c.Type))
                {
                    sb.Append(c.Type).Append(':').Append(c.Value).Append('|');
                }
                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                using var sha = SHA256.Create();
                var hash = sha.ComputeHash(bytes);
                return Convert.ToHexString(hash);
            }
            catch
            {
                return null;
            }
        }

        public bool ValidateAdditionalData(HttpContext context, string additionalData)
        {
            var expected = GetAdditionalData(context);
            // allow null/empty comparisons
            return string.Equals(expected ?? string.Empty, additionalData ?? string.Empty, StringComparison.Ordinal);
        }
    }
}
