using HMS.UI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using HMS.UI.Models.Auth;

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


        [HttpGet("login")]
        // Allow optional tenantCode via route (/login/{tenantCode})
        public IActionResult Login(
    string? tenantCode = null,
    string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!string.IsNullOrWhiteSpace(tenantCode))
                ViewData["TenantCode"] = tenantCode;

            return View();
        }
        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login( string username, string password, string? tenantCode, string? returnUrl = null)
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
                    var root = doc.RootElement;

                    // If API returns wrapper { success, status, data }, unwrap to data
                    if (root.ValueKind == System.Text.Json.JsonValueKind.Object && root.TryGetProperty("success", out var succ))
                    {
                        if (root.TryGetProperty("data", out var data) && data.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            root = data;
                        }
                    }

                    var claims = new System.Collections.Generic.List<System.Security.Claims.Claim>();
                    string? apiUsername = null;
                    string? displayName = null;
                    
                    if (root.TryGetProperty("userId", out var u) && u.ValueKind == System.Text.Json.JsonValueKind.String)
                        claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, u.GetString() ?? string.Empty));

                    if (root.TryGetProperty("tenant", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.Object && t.TryGetProperty("name", out var tn) && tn.ValueKind == System.Text.Json.JsonValueKind.String)
                        claims.Add(new System.Security.Claims.Claim("tenant_name", tn.GetString() ?? string.Empty));

                    if (root.TryGetProperty("username", out var userNameProp) && userNameProp.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        apiUsername = userNameProp.GetString();
                    }

                    if (root.TryGetProperty("displayName", out var displayNameProp) && displayNameProp.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        displayName = displayNameProp.GetString();
                    }

                    // permissions property may be camelCase or PascalCase depending on serializer settings; find case-insensitively
                    System.Text.Json.JsonElement? permsElem = null;
                    try
                    {
                        foreach (var prop in root.EnumerateObject())
                        {
                            if (string.Equals(prop.Name, "permissions", StringComparison.OrdinalIgnoreCase))
                            {
                                permsElem = prop.Value;
                                break;
                            }
                        }
                    }
                    catch { }

                    if (permsElem.HasValue && permsElem.Value.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var p in permsElem.Value.EnumerateArray())
                        {
                            var pv = p.GetString();
                            if (!string.IsNullOrWhiteSpace(pv)) claims.Add(new System.Security.Claims.Claim("permission", pv));
                        }
                    }

                    // Also include apiUsername/name if present for antiforgery and display purposes
                    if (!string.IsNullOrWhiteSpace(apiUsername))
                    {
                        claims.Add(new Claim("username", apiUsername));
                    }

                    var effectiveDisplayName = string.IsNullOrWhiteSpace(displayName)
                        ? (string.IsNullOrWhiteSpace(apiUsername) ? null : apiUsername)
                        : displayName;

                    if (!string.IsNullOrWhiteSpace(effectiveDisplayName))
                    {
                        claims.Add(new Claim("display_name", effectiveDisplayName));
                        claims.Add(new Claim(ClaimTypes.Name, effectiveDisplayName));
                    }

                    // Ensure there is a Name claim (some flows expect it). If missing, use username then userId.
                    if (!claims.Any(c => c.Type == ClaimTypes.Name))
                    {
                        if (!string.IsNullOrWhiteSpace(apiUsername))
                        {
                            claims.Add(new Claim(ClaimTypes.Name, apiUsername));
                        }
                        else if (root.TryGetProperty("userId", out var u2) && u2.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            claims.Add(new Claim(ClaimTypes.Name, u2.GetString() ?? string.Empty));
                        }
                    }

                    var id = new System.Security.Claims.ClaimsIdentity(claims, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new System.Security.Claims.ClaimsPrincipal(id);

                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(15)
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        principal,
                        authProperties);
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

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var resetUrl = $"{Request.Scheme}://{Request.Host}/Account/ResetPassword";
                var headers = new Dictionary<string, string> { ["X-Reset-Url"] = resetUrl };
                await _api.PostRawAsync("/auth/forgot-password", new { model.Email }, headers);
                TempData["Success"] = "If the email exists, a recovery link has been sent.";
                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(model);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(string token)
        {
            var vm = new ResetPasswordViewModel { Token = token };

            if (string.IsNullOrWhiteSpace(token))
            {
                vm.IsValid = false;
                TempData["Error"] = "Recovery token is required.";
                return View(vm);
            }

            try
            {
                var validation = await _api.GetAsync<System.Text.Json.JsonElement>($"/auth/password-reset/validate?token={Uri.EscapeDataString(token)}");
                if (validation.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (validation.TryGetProperty("valid", out var validProp) &&
                        (validProp.ValueKind == System.Text.Json.JsonValueKind.True || validProp.ValueKind == System.Text.Json.JsonValueKind.False))
                    {
                        vm.IsValid = validProp.GetBoolean();
                    }

                    if (validation.TryGetProperty("username", out var userProp) && userProp.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        vm.Username = userProp.GetString();
                    }
                }

                if (!vm.IsValid)
                {
                    TempData["Error"] = "This recovery link is invalid or has expired.";
                }
            }
            catch
            {
                vm.IsValid = false;
                TempData["Error"] = "This recovery link is invalid or has expired.";
            }

            return View(vm);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.IsValid = true;
                return View(model);
            }

            try
            {
                var response = await _api.PostRawAsync("/auth/reset-password", new { model.Token, model.NewPassword });
                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = "Password reset failed.";
                    model.IsValid = true;
                    return View(model);
                }

                TempData["Success"] = "Password reset successful. Sign in with your new password.";
                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                model.IsValid = true;
                return View(model);
            }
        }
    }
}
