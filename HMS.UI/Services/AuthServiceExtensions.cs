using System.Net.Http.Headers;
using System.Text.Json;
using HMS.UI.Models.Auth;

namespace HMS.UI.Services;

public static class AuthServiceExtensions
{
    public static async Task<bool> TrySetAuthCookieFromResponseAsync(this ApiClient client, HttpResponseMessage resp, HttpContext httpContext)
    {
        if (resp == null || httpContext == null) return false;

        // If API already sets cookie header, nothing to do
        if (resp.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            try
            {
                // Copy Set-Cookie headers from API response to UI response so browser receives them
                foreach (var sc in setCookies)
                {
                    httpContext.Response.Headers.Append("Set-Cookie", sc);
                }
            }
            catch { }
            return true;
        }

        try
        {
            var body = await resp.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(body)) return false;

            using var doc = JsonDocument.Parse(body);

            // access token (camelCase or PascalCase)
            JsonElement atElem;
            if (!doc.RootElement.TryGetProperty("accessToken", out atElem) && !doc.RootElement.TryGetProperty("AccessToken", out atElem))
                return false;

            var accessToken = atElem.GetString();
            if (string.IsNullOrWhiteSpace(accessToken)) return false;

            // refresh token (optional)
            JsonElement rtElem;
            var refreshToken = string.Empty;
            if (doc.RootElement.TryGetProperty("refreshToken", out rtElem) || doc.RootElement.TryGetProperty("RefreshToken", out rtElem))
            {
                refreshToken = rtElem.GetString() ?? string.Empty;
            }

            // expiresAt (optional)
            DateTimeOffset? expires = null;
            JsonElement expElem;
            if (doc.RootElement.TryGetProperty("expiresAt", out expElem) || doc.RootElement.TryGetProperty("ExpiresAt", out expElem))
            {
                if (expElem.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(expElem.GetString(), out var dt)) expires = dt;
                else if (expElem.ValueKind == JsonValueKind.Number && expElem.TryGetInt64(out var num)) expires = DateTimeOffset.FromUnixTimeSeconds(num);
            }

            var cookieOptions = new CookieOptions { HttpOnly = true, Secure = httpContext.Request.IsHttps, SameSite = SameSiteMode.Strict, Path = "/" };
            if (expires.HasValue) cookieOptions.Expires = expires.Value.UtcDateTime;

            httpContext.Response.Cookies.Append("HmsAuth", accessToken, cookieOptions);

            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                // refresh token typically longer-lived
                var rtOptions = new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict };
                // set same expiry as provided or a sensible default (7 days)
                rtOptions.Expires = expires?.UtcDateTime ?? DateTimeOffset.UtcNow.AddDays(7).UtcDateTime;
                httpContext.Response.Cookies.Append("HmsRefresh", refreshToken, rtOptions);
            }

            // tenant name (optional) - API returns nested tenant object
            try
            {
                if (doc.RootElement.TryGetProperty("tenant", out var tenantElem) || doc.RootElement.TryGetProperty("Tenant", out tenantElem))
                {
                    if (tenantElem.ValueKind == JsonValueKind.Object && tenantElem.TryGetProperty("name", out var nameElem))
                    {
                        var tname = nameElem.GetString();
                        if (!string.IsNullOrWhiteSpace(tname))
                        {
                            var tnOptions = new CookieOptions { HttpOnly = false, Secure = httpContext.Request.IsHttps, SameSite = SameSiteMode.Strict, Path = "/" };
                            httpContext.Response.Cookies.Append("HmsTenantName", tname, tnOptions);
                        }
                    }
                }
            }
            catch { }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
