using System.Threading.Tasks;
using HMS.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace HMS.UI.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApiClient _api;
        private readonly RefreshService _refresh;
        private readonly IConfiguration _config;

        public AccountController(ApiClient api, RefreshService refresh, IConfiguration config)
        {
            _api = api;
            _refresh = refresh;
            _config = config;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            // Attempt to load minimal profile info for the dashboard
            try
            {
                var profile = await _api.GetAsync<HMS.UI.Models.Profile.UserProfileViewModel>("/api/Profile/me");
                var tenantName = Request.Cookies["HmsTenantName"] ?? string.Empty;
                // If cookie missing, try fetch tenant name by tenant id cookie
                if (string.IsNullOrWhiteSpace(tenantName))
                {
                    try
                    {
                        var tenantId = Request.Cookies["HmsTenantId"];
                        if (!string.IsNullOrWhiteSpace(tenantId))
                        {
                            var t = await _api.GetAsync<object>($"/tenants/{tenantId}");
                            if (t is System.Text.Json.JsonElement te && te.TryGetProperty("name", out var n)) tenantName = n.GetString() ?? string.Empty;
                        }
                    }
                    catch {
                        TempData["Error"] = "Unable to read tenant information. Please ensure the application is properly configured.";
                    }
                }

                ViewData["TenantName"] = tenantName;
                ViewData["ProfileFirstName"] = profile?.FirstName ?? string.Empty;
            }
            catch { }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Read refresh token from cookie and send to API to revoke
            var refresh = Request.Cookies["HmsRefresh"] ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(refresh))
            {
                try
                {
                    await _api.PostAsync<object>("/auth/logout", new { RefreshToken = refresh });
                }
                catch { }
            }

            // Clear API cookies
            Response.Cookies.Delete("HmsAuth");
            Response.Cookies.Delete("HmsRefresh");
            Response.Cookies.Delete("HmsTenantName");
            Response.Cookies.Delete("HmsTenantId");

            // Sign out local UI cookie
            try
            {
                await HttpContext.SignOutAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
            }
            catch { }

            return RedirectToAction("Login");
        }

        // Parameter-injected constructor for DI when RefreshService available
     

        [HttpGet]
        // Allow optional tenantCode via route (/login/{tenantCode})
        public IActionResult Login(string? tenantCode = null)
        {
            if (!string.IsNullOrWhiteSpace(tenantCode)) ViewData["TenantCode"] = tenantCode;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, string tenantCode)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(string.Empty, "Username and password required");
                return View();
            }

            var payload = new { Username = username, Password = password };

            try
            {
                // Use PostRawAsync so we can inspect non-success responses without throwing
                // If a tenant code was supplied prefer header X-Tenant-Code for server to resolve tenant id
                System.Collections.Generic.IDictionary<string, string>? headers = null;
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    headers = new System.Collections.Generic.Dictionary<string, string> { ["X-Tenant-Code"] = tenantCode };
                }

                var resp = await _api.PostRawAsync("/auth/login", payload, headers);

                if (!resp.IsSuccessStatusCode)
                {
                    var error = await resp.Content.ReadAsStringAsync();
                    // Log or surface a friendly error
                    ModelState.AddModelError(string.Empty, "Invalid credentials");
                    return View();
                }

                // On success set auth cookies if returned and sign in local cookie for UI
                await _api.TrySetAuthCookieFromResponseAsync(resp, HttpContext);

                // create local auth cookie using claims from response body if available
                try
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(body);
                    var claims = new System.Collections.Generic.List<System.Security.Claims.Claim>();
                    if (doc.RootElement.TryGetProperty("userId", out var u)) claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, u.GetString() ?? string.Empty));
                    if (doc.RootElement.TryGetProperty("tenant", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.Object && t.TryGetProperty("name", out var tn)) claims.Add(new System.Security.Claims.Claim("tenant_name", tn.GetString() ?? string.Empty));
                    if (doc.RootElement.TryGetProperty("permissions", out var perms) && perms.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var p in perms.EnumerateArray())
                        {
                            var pv = p.GetString();
                            if (!string.IsNullOrWhiteSpace(pv)) claims.Add(new System.Security.Claims.Claim("permission", pv));
                        }
                    }

                    var id = new System.Security.Claims.ClaimsIdentity(claims, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new System.Security.Claims.ClaimsPrincipal(id);
                    await HttpContext.SignInAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme, principal);
                }
                catch { }

                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                // API client may throw for unexpected errors; show a friendly message
                ModelState.AddModelError(string.Empty, "Login failed. " + ex.Message);
                return View();
            }
        }
    }
}
