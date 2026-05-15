using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;

namespace HMS.UI.Services;

/// <summary>
/// ApiClient for making HTTP requests to the API. Note: tenantId should not be included in the payload.
public class ApiClient
{
    private readonly IHttpClientFactory _factory;
    private readonly IHttpContextAccessor _ctx;
    // lightweight in-memory debug info (development only)
    private ApiClientDebugInfo? _lastDebug;
    // global last debug across requests (dev-only helper)
    private static ApiClientDebugInfo? _globalLastDebug;

    public ApiClient(IHttpClientFactory factory, IHttpContextAccessor ctx)
    {
        _factory = factory;
        _ctx = ctx;
    }

    public ApiClientDebugInfo? GetLastDebug() => _lastDebug ?? _globalLastDebug;

    public async Task<T?> GetAsync<T>(string path)
    {
        var client = CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        try
        {
            var incomingHost = GetIncomingHostWithoutPort();
            if (!string.IsNullOrWhiteSpace(incomingHost))
            {
                req.Headers.Host = incomingHost;
                // also forward X-Forwarded-Host for servers that read it
                if (!req.Headers.Contains("X-Forwarded-Host")) req.Headers.TryAddWithoutValidation("X-Forwarded-Host", incomingHost);
            }
        }
        catch { }

        await CaptureDebugRequestAsync(req, null);

        var res = await client.SendAsync(req);
        await CaptureDebugResponseAsync(res);

        if (!res.IsSuccessStatusCode)
        {
            // Treat common non-success responses as non-exceptional for UI flows
            if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized || res.StatusCode == System.Net.HttpStatusCode.Forbidden || res.StatusCode == System.Net.HttpStatusCode.NotFound)
                return default;

            var error = await res.Content.ReadAsStringAsync();
            throw new Exception($"API Error: {res.StatusCode} - {error}");
        }

        if (res.Content.Headers.ContentLength == 0) return default;
        //return await res.Content.ReadFromJsonAsync<T>();
        return await res.Content.ReadFromJsonAsync<T>(new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    public async Task<HttpResponseMessage> PutRawAsync(string path, object payload)
    {
        var client = CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Put, path);
        req.Content = JsonContent.Create(payload);

        // Propagate original request host so API can resolve tenant based on client host
        try
        {
            var incomingHost = GetIncomingHostWithoutPort();
            if (!string.IsNullOrWhiteSpace(incomingHost))
            {
                req.Headers.Host = incomingHost;
                if (!req.Headers.Contains("X-Forwarded-Host")) req.Headers.TryAddWithoutValidation("X-Forwarded-Host", incomingHost);
            }
        }
        catch { }

        await CaptureDebugRequestAsync(req, payload);
        var res = await client.SendAsync(req);
        await CaptureDebugResponseAsync(res);
        return res;
    }

    public async Task<T?> PutAsync<T>(string path, object payload)
    {
        var client = CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Put, path);
        req.Content = JsonContent.Create(payload);
        try
        {
            var incomingHost = GetIncomingHostWithoutPort();
            if (!string.IsNullOrWhiteSpace(incomingHost))
            {
                req.Headers.Host = incomingHost;
                if (!req.Headers.Contains("X-Forwarded-Host")) req.Headers.TryAddWithoutValidation("X-Forwarded-Host", incomingHost);
            }
        }
        catch { }

        await CaptureDebugRequestAsync(req, payload);
        var res = await client.SendAsync(req);
        await CaptureDebugResponseAsync(res);

        if (!res.IsSuccessStatusCode)
        {
            if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized || res.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return default;

            var error = await res.Content.ReadAsStringAsync();
            throw new Exception($"API Error: {res.StatusCode} - {error}");
        }

        if (res.Content.Headers.ContentLength == 0) return default;
        return await res.Content.ReadFromJsonAsync<T>(new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient("HmsApi");
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        var token = GetAuthTokenFromCookie();
        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }


        return client;
    }

    private string GetAuthTokenFromCookie()
    {
        try
        {
            return _ctx.HttpContext?.Request?.Cookies["HmsAuth"] ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    // Return incoming host without port (e.g. "example.com" not "example.com:5000")
    private string? GetIncomingHostWithoutPort()
    {
        try
        {
            // prefer Host.Host which excludes port
            var hostOnly = _ctx.HttpContext?.Request?.Host.Host;
            if (!string.IsNullOrWhiteSpace(hostOnly)) return hostOnly;

            var raw = _ctx.HttpContext?.Request?.Host.Value;
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var idx = raw.IndexOf(':');
            return idx > 0 ? raw.Substring(0, idx) : raw;
        }
        catch { return null; }
    }

    private async Task CaptureDebugRequestAsync(HttpRequestMessage req, object? payload)
    {
        try
        {
            var headers = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var h in req.Headers)
            {
                headers[h.Key] = string.Join(',', h.Value);
            }
            // include content headers (some callers may add custom headers here)
            if (req.Content?.Headers != null)
            {
                foreach (var h in req.Content.Headers)
                {
                    headers[h.Key] = string.Join(',', h.Value);
                }
            }

            var body = string.Empty;
            if (payload != null)
            {
                body = System.Text.Json.JsonSerializer.Serialize(payload);
            }
            else if (req.Content != null)
            {
                body = await req.Content.ReadAsStringAsync();
            }

            _lastDebug = new ApiClientDebugInfo
            {
                Method = req.Method.Method,
                Url = req.RequestUri?.ToString() ?? string.Empty,
                HostHeader = req.Headers.Host ?? string.Empty,
                RequestHeaders = headers,
                RequestBody = body
            };
            try { _globalLastDebug = _lastDebug; } catch { }
        }
        catch { }
    }

    private async Task CaptureDebugResponseAsync(HttpResponseMessage res)
    {
        try
        {
            var headers = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var h in res.Headers)
            {
                headers[h.Key] = string.Join(',', h.Value);
            }

            var body = res.Content != null ? await res.Content.ReadAsStringAsync() : string.Empty;
            if (_lastDebug == null) _lastDebug = new ApiClientDebugInfo();
            _lastDebug.ResponseStatus = (int)res.StatusCode;
            _lastDebug.ResponseHeaders = headers;
            _lastDebug.ResponseBody = body;
            try { _globalLastDebug = _lastDebug; } catch { }
        }
        catch { }
    }

    public class ApiClientDebugInfo
    {
        public string Method { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string HostHeader { get; set; } = string.Empty;
        public System.Collections.Generic.Dictionary<string, string>? RequestHeaders { get; set; }
        public string? RequestBody { get; set; }
        public int? ResponseStatus { get; set; }
        public System.Collections.Generic.Dictionary<string, string>? ResponseHeaders { get; set; }
        public string? ResponseBody { get; set; }
    }

    public async Task<T?> PostAsync<T>(string path, object payload)
    {
        var client = CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, path);
        req.Content = JsonContent.Create(payload);
        try
        {
            var incomingHost = GetIncomingHostWithoutPort();
            if (!string.IsNullOrWhiteSpace(incomingHost))
            {
                req.Headers.Host = incomingHost;
                if (!req.Headers.Contains("X-Forwarded-Host")) req.Headers.TryAddWithoutValidation("X-Forwarded-Host", incomingHost);
            }
        }
        catch { }

        await CaptureDebugRequestAsync(req, payload);
        var res = await client.SendAsync(req);
        await CaptureDebugResponseAsync(res);

        if (!res.IsSuccessStatusCode)
        {
            // For authentication failures return null so callers can show a friendly message
            if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized || res.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return default;

            var error = await res.Content.ReadAsStringAsync();
            throw new Exception($"API Error: {res.StatusCode} - {error}");
        }

        // Handle empty body
        if (res.Content.Headers.ContentLength == 0)
            return default;

        return await res.Content.ReadFromJsonAsync<T>();
    }
    public async Task<HttpResponseMessage> PostRawAsync(string path, object payload)
    {
        var client = CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, path);
        req.Content = JsonContent.Create(payload);

        // Propagate original request host so API can resolve tenant based on client host
        try
        {
            var incomingHost = GetIncomingHostWithoutPort();
            if (!string.IsNullOrWhiteSpace(incomingHost))
            {
                req.Headers.Host = incomingHost;
                if (!req.Headers.Contains("X-Forwarded-Host")) req.Headers.TryAddWithoutValidation("X-Forwarded-Host", incomingHost);
            }
        }
        catch { }

        await CaptureDebugRequestAsync(req, payload);
        var res = await client.SendAsync(req);
        await CaptureDebugResponseAsync(res);
        return res;
    }

    // Overload allowing custom headers for the outgoing request (used for tenant-code header)
    public async Task<HttpResponseMessage> PostRawAsync(string path, object payload, System.Collections.Generic.IDictionary<string, string>? headers)
    {
        var client = CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, path);
        req.Content = JsonContent.Create(payload);

        // Propagate original request host so API can resolve tenant based on client host
        try
        {
            var incomingHost = GetIncomingHostWithoutPort();
            if (!string.IsNullOrWhiteSpace(incomingHost))
            {
                req.Headers.Host = incomingHost;
                if (!req.Headers.Contains("X-Forwarded-Host")) req.Headers.TryAddWithoutValidation("X-Forwarded-Host", incomingHost);
            }
        }
        catch { }

        if (headers != null)
        {
            foreach (var kv in headers)
            {
                // Add header to request (avoid adding restricted headers)
                if (!string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                    req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }
        }

        await CaptureDebugRequestAsync(req, payload);
        var res = await client.SendAsync(req);
        await CaptureDebugResponseAsync(res);
        return res;
    }

    // Inspect auth response body and set API cookies (HmsAuth, HmsRefresh, tenant info) in the UI HttpContext
    public async Task TrySetAuthCookieFromResponseAsync(HttpResponseMessage resp, Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        if (resp == null || httpContext == null) return;
        var body = await resp.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body)) return;

        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var root = doc.RootElement;

        string? accessToken = null;
        string? refreshToken = null;

        if (root.TryGetProperty("accessToken", out var at) && at.ValueKind == System.Text.Json.JsonValueKind.String)
            accessToken = at.GetString();
        else if (root.TryGetProperty("AccessToken", out at) && at.ValueKind == System.Text.Json.JsonValueKind.String)
            accessToken = at.GetString();

        if (root.TryGetProperty("refreshToken", out var rt) && rt.ValueKind == System.Text.Json.JsonValueKind.String)
            refreshToken = rt.GetString();
        else if (root.TryGetProperty("RefreshToken", out rt) && rt.ValueKind == System.Text.Json.JsonValueKind.String)
            refreshToken = rt.GetString();

        var cookieOptions = new Microsoft.AspNetCore.Http.CookieOptions { HttpOnly = true, Secure = httpContext.Request.IsHttps, SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict };

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            httpContext.Response.Cookies.Append("HmsAuth", accessToken, cookieOptions);
        }

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var rtOptions = new Microsoft.AspNetCore.Http.CookieOptions { HttpOnly = true, Secure = httpContext.Request.IsHttps, SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict, Expires = System.DateTimeOffset.UtcNow.AddDays(7).UtcDateTime };
            httpContext.Response.Cookies.Append("HmsRefresh", refreshToken, rtOptions);
        }

        // tenant info may be returned as object 'tenant' or tenantId/tenant
        if (root.TryGetProperty("tenant", out var tenantElem) && tenantElem.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            if (tenantElem.TryGetProperty("id", out var id) && id.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                httpContext.Response.Cookies.Append("HmsTenantId", id.GetString() ?? string.Empty, cookieOptions);
            }
            if (tenantElem.TryGetProperty("name", out var tn) && tn.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                httpContext.Response.Cookies.Append("HmsTenantName", tn.GetString() ?? string.Empty, cookieOptions);
            }
        }
        else
        {
            if (root.TryGetProperty("tenantId", out var tid) && tid.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                httpContext.Response.Cookies.Append("HmsTenantId", tid.GetString() ?? string.Empty, cookieOptions);
            }
            else if (root.TryGetProperty("TenantId", out var tid2) && tid2.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                httpContext.Response.Cookies.Append("HmsTenantId", tid2.GetString() ?? string.Empty, cookieOptions);
            }

            if (root.TryGetProperty("tenant", out var tname) && tname.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                httpContext.Response.Cookies.Append("HmsTenantName", tname.GetString() ?? string.Empty, cookieOptions);
            }
        }

        // If we have a tenant id but no tenant name, try to fetch tenant details from API and set name cookie
        try
        {
            string? tenantId = null;
            if (root.TryGetProperty("tenant", out var tObj) && tObj.ValueKind == System.Text.Json.JsonValueKind.Object && tObj.TryGetProperty("id", out var idp) && idp.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                tenantId = idp.GetString();
            }
            else if (root.TryGetProperty("tenantId", out var tidx) && tidx.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                tenantId = tidx.GetString();
            }

            var tenantNameExists = !string.IsNullOrWhiteSpace(httpContext.Request.Cookies["HmsTenantName"]);
            if (!tenantNameExists && !string.IsNullOrWhiteSpace(tenantId))
            {
                var client = _factory.CreateClient("HmsApi");
                var tenantResp = await client.GetAsync($"/tenants/{tenantId}");
                if (tenantResp.IsSuccessStatusCode)
                {
                    var json = await tenantResp.Content.ReadAsStringAsync();
                    try
                    {
                        using var jd = System.Text.Json.JsonDocument.Parse(json);
                        var rootElem = jd.RootElement;
                        if (rootElem.TryGetProperty("name", out var n) && n.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            httpContext.Response.Cookies.Append("HmsTenantName", n.GetString() ?? string.Empty, cookieOptions);
                        }
                    }
                    catch { }
                }
            }
                // If we still don't have a tenant name but we had a tenant id, set a short-lived flag cookie
                var tenantNameNow = httpContext.Request.Cookies["HmsTenantName"] ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(tenantId) && string.IsNullOrWhiteSpace(tenantNameNow))
                {
                    // set a marker so UI can show a helpful message
                    var flagOpts = new Microsoft.AspNetCore.Http.CookieOptions { HttpOnly = false, Secure = httpContext.Request.IsHttps, SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax };
                    httpContext.Response.Cookies.Append("HmsTenantNameResolveFailed", "1", flagOpts);
                }
                else
                {
                    // clear any previous flag when name resolved
                    try
                    {
                        httpContext.Response.Cookies.Delete("HmsTenantNameResolveFailed");
                    }
                    catch { }
                }
        }
        catch { }
    }
}
