using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace HMS.UI.Services;

public class RefreshService
{
    private readonly ApiClient _api;
    private readonly IHttpContextAccessor _ctx;

    public RefreshService(ApiClient api, IHttpContextAccessor ctx)
    {
        _api = api;
        _ctx = ctx;
    }

    public async Task<bool> TryRefreshAsync()
    {
        var client = _api; // use api client to post refresh
        try
        {
            var http = typeof(ApiClient).GetMethod("CreateClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(_api, null) as HttpClient;
            if (http == null) return false;

            // Read refresh token cookie
            var refresh = _ctx.HttpContext?.Request.Cookies["HmsRefresh"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(refresh)) return false;

            var payload = new { RefreshToken = refresh };
            var resp = await http.PostAsJsonAsync("/auth/refresh", payload);
            if (!resp.IsSuccessStatusCode) return false;

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("accessToken", out var at) && !doc.RootElement.TryGetProperty("AccessToken", out at)) return false;
            var accessToken = at.GetString();
            if (string.IsNullOrWhiteSpace(accessToken)) return false;

            // Optionally update refresh token
            if (doc.RootElement.TryGetProperty("refreshToken", out var rt))
            {
                var refreshNew = rt.GetString();
                if (!string.IsNullOrWhiteSpace(refreshNew))
                {
                    var rtOptions = new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Expires = DateTimeOffset.UtcNow.AddDays(7).UtcDateTime };
                    _ctx.HttpContext?.Response.Cookies.Append("HmsRefresh", refreshNew, rtOptions);
                }
            }

            var atOptions = new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict };
            _ctx.HttpContext?.Response.Cookies.Append("HmsAuth", accessToken, atOptions);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
