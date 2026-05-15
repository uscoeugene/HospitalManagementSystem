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

                // Prefer tenant resolved by middleware (HttpContext.Items["TenantId"]) then fall back to tenant cookie
                string tenantName = Request.Cookies["HmsTenantName"] ?? string.Empty;
                Guid? resolvedTid = null;
                try
                {
                    if (HttpContext.Items.TryGetValue("TenantId", out var tv) && tv is Guid g)
                    {
                        resolvedTid = g;
                    }
                    else
                    {
                        var tenantIdCookie = Request.Cookies["HmsTenantId"];
                        if (!string.IsNullOrWhiteSpace(tenantIdCookie) && Guid.TryParse(tenantIdCookie, out var parsed)) resolvedTid = parsed;
                    }
                }
                catch { }

                if (string.IsNullOrWhiteSpace(tenantName) && resolvedTid.HasValue)
                {
                    try
                    {
                        var t = await _api.GetAsync<object>($"/tenants/{resolvedTid.Value}");
                        if (t is System.Text.Json.JsonElement te && te.TryGetProperty("name", out var n)) tenantName = n.GetString() ?? string.Empty;
                        TempData["Info"] = "Tenant information loaded from API based on resolved tenant id.";

                        // ensure cookies are set for subsequent requests
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(tenantName))
                            {
                                Response.Cookies.Append("HmsTenantName", tenantName, new Microsoft.AspNetCore.Http.CookieOptions { HttpOnly = false, SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax });
                                Response.Cookies.Append("HmsTenantId", resolvedTid.Value.ToString(), new Microsoft.AspNetCore.Http.CookieOptions { HttpOnly = false, SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax });
                            }
                        }
                        catch { }
                    }
                    catch
                    {
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

                //if (!resp.IsSuccessStatusCode)
                //{
                //    var error = await resp.Content.ReadAsStringAsync();
                //    try
                //    {
                //        var dbg = _api.GetLastDebug();
                //        if (dbg != null)
                //        {
                //            TempData["ApiDebug"] = System.Text.Json.JsonSerializer.Serialize(dbg, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                //        }
                //    }
                //    catch { }
                //    // Log or surface a friendly error
                //    ModelState.AddModelError(string.Empty, resp.Content. + "Invalid credentials");
                //    return View();
                //}

                if (!resp.IsSuccessStatusCode)
                {
                    var errorContent = await resp.Content.ReadAsStringAsync();

                    string errorMessage = "Login failed.";

                    try
                    {
                        using var errorDoc = System.Text.Json.JsonDocument.Parse(errorContent);

                        // Prefer "detail" from API response
                        if (errorDoc.RootElement.TryGetProperty("detail", out var detail))
                        {
                            errorMessage = detail.GetString() ?? errorMessage;
                        }
                        // fallback to title
                        else if (errorDoc.RootElement.TryGetProperty("title", out var title))
                        {
                            errorMessage = title.GetString() ?? errorMessage;
                        }
                    }
                    catch
                    {
                        // fallback if response is not valid JSON
                        if (!string.IsNullOrWhiteSpace(errorContent))
                        {
                            errorMessage = errorContent;
                        }
                    }

                    try
                    {
                        var dbg = _api.GetLastDebug();
                        if (dbg != null)
                        {
                            TempData["ApiDebug"] = System.Text.Json.JsonSerializer.Serialize(
                                dbg,
                                new System.Text.Json.JsonSerializerOptions
                                {
                                    WriteIndented = true
                                });
                        }
                    }
                    catch { }

                    ModelState.AddModelError(string.Empty, errorMessage);

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
                try
                {
                    var dbg = _api.GetLastDebug();
                    if (dbg != null) TempData["ApiDebug"] = System.Text.Json.JsonSerializer.Serialize(dbg, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                }
                catch { }
                ModelState.AddModelError(string.Empty, "Login failed. " + ex.Message);
                return View();
            }
        }
    }
}
