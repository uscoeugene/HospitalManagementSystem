using HMS.UI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HMS.UI.Models.Auth;
using System.Text.Json;

namespace HMS.UI.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApiClient _api;
        private readonly RefreshService _refresh;
        private readonly IHostEnvironment _env;
        private readonly IDeploymentModeService _deploymentModeService;

        public AccountController(ApiClient api, RefreshService refresh, IHostEnvironment env, IDeploymentModeService deploymentModeService)
        {
            _api = api;
            _refresh = refresh;
            _env = env;
            _deploymentModeService = deploymentModeService;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                return View(await BuildDashboardViewModelAsync());
            }
            catch
            {
                return View(new HMS.UI.Models.DashboardViewModel
                {
                    DisplayName = User.Identity?.Name ?? "User",
                    TenantName = Request.Cookies["HmsTenantName"] ?? string.Empty,
                    DeploymentMode = await ResolveDeploymentModeAsync(),
                    IsDevelopment = _env.IsDevelopment(),
                    Roles = GetCurrentRoles()
                });
            }
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

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefreshPermissionCatalog()
        {
            if (!_env.IsDevelopment())
            {
                TempData["Error"] = "Permission refresh is only available in development.";
                return RedirectToAction(nameof(Dashboard));
            }

            try
            {
                var response = await _api.PostRawAsync("/admin/seed/permissions", new { });
                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = "Permission refresh failed. Check the API debug output for details.";
                    return RedirectToAction(nameof(Dashboard));
                }

                TempData["Success"] = "Permission catalog refreshed successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Permission refresh failed. " + ex.Message;
            }

            return RedirectToAction(nameof(Dashboard));
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

                    System.Text.Json.JsonElement? rolesElem = null;
                    try
                    {
                        foreach (var prop in root.EnumerateObject())
                        {
                            if (string.Equals(prop.Name, "roles", StringComparison.OrdinalIgnoreCase))
                            {
                                rolesElem = prop.Value;
                                break;
                            }
                        }
                    }
                    catch { }

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

                    if (rolesElem.HasValue && rolesElem.Value.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var role in rolesElem.Value.EnumerateArray())
                        {
                            var rv = role.GetString();
                            if (!string.IsNullOrWhiteSpace(rv))
                            {
                                claims.Add(new Claim(ClaimTypes.Role, rv));
                                claims.Add(new Claim("role", rv));
                            }
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

        private async Task<HMS.UI.Models.DashboardViewModel> BuildDashboardViewModelAsync()
        {
            var roles = GetCurrentRoles();
            var displayName = User.FindFirst("display_name")?.Value
                ?? User.Identity?.Name
                ?? "User";

            try
            {
                var profile = await _api.GetAsync<HMS.UI.Models.Profile.UserProfileViewModel>("/api/Profile/me");
                if (!string.IsNullOrWhiteSpace(profile?.FirstName))
                {
                    displayName = profile.FirstName;
                }
            }
            catch { }

            var tenantName = await ResolveTenantNameAsync();
            var queueCounts = await LoadQueueCountsAsync();
            var deploymentMode = await ResolveDeploymentModeAsync();

            return new HMS.UI.Models.DashboardViewModel
            {
                DisplayName = displayName,
                TenantName = tenantName,
                DeploymentMode = deploymentMode,
                IsDevelopment = _env.IsDevelopment(),
                Roles = roles,
                RoleCards = BuildRoleCards(roles),
                QueueCards = BuildQueueCards(roles, queueCounts),
                QuickActions = BuildQuickActions(roles)
            };
        }

        private string[] GetCurrentRoles()
        {
            return User.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v)
                .ToArray();
        }

        private async Task<string> ResolveTenantNameAsync()
        {
            var tenantName = Request.Cookies["HmsTenantName"] ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(tenantName))
            {
                return tenantName;
            }

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
                    if (!string.IsNullOrWhiteSpace(tenantIdCookie) && Guid.TryParse(tenantIdCookie, out var parsed))
                    {
                        resolvedTid = parsed;
                    }
                }
            }
            catch { }

            if (!resolvedTid.HasValue)
            {
                return string.Empty;
            }

            try
            {
                var tenant = await _api.GetAsync<object>($"/tenants/{resolvedTid.Value}");
                if (tenant is System.Text.Json.JsonElement json && json.TryGetProperty("name", out var name) && name.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    tenantName = name.GetString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(tenantName))
                    {
                        Response.Cookies.Append("HmsTenantName", tenantName, new Microsoft.AspNetCore.Http.CookieOptions { HttpOnly = false, SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax });
                        Response.Cookies.Append("HmsTenantId", resolvedTid.Value.ToString(), new Microsoft.AspNetCore.Http.CookieOptions { HttpOnly = false, SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax });
                    }
                }
            }
            catch
            {
                TempData["Error"] = "Unable to read Hospital information. Please ensure the application is properly configured.";
            }

            return tenantName;
        }

        private async Task<System.Collections.Generic.Dictionary<string, int>> LoadQueueCountsAsync()
        {
            var counts = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            async Task AddCountAsync<T>(string key, string path)
            {
                try
                {
                    var page = await _api.GetAsync<HMS.UI.Models.PagedResult<T>>(path);
                    counts[key] = page?.TotalCount ?? page?.Items?.Length ?? 0;
                }
                catch
                {
                    counts[key] = 0;
                }
            }

            await Task.WhenAll(
                AddCountAsync<HMS.UI.Models.Lab.LabRequestViewModel>("lab", "/lab/requests?status=PENDING&page=1&pageSize=1"),
                AddCountAsync<HMS.UI.Models.Pharmacy.PrescriptionViewModel>("pharmacy", "/pharmacy/prescriptions?status=PENDING&page=1&pageSize=1"),
                AddCountAsync<HMS.UI.Models.Billing.InvoiceViewModel>("billing", "/billing?status=UNPAID&page=1&pageSize=1"),
                AddCountAsync<HMS.UI.Models.Billing.DebtViewModel>("debt", "/billing/debts?unresolvedOnly=true&page=1&pageSize=1")
            );

            return counts;
        }

        private async Task<string> ResolveDeploymentModeAsync()
        {
            return await _deploymentModeService.GetEffectiveModeAsync();
        }

        private static HMS.UI.Models.DashboardCardViewModel[] BuildRoleCards(string[] roles)
        {
            var normalized = roles.Select(NormalizeRoleKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var cards = new System.Collections.Generic.List<HMS.UI.Models.DashboardCardViewModel>();

            if (HasRole(normalized, "doctor", "physician", "clinical"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Clinical Workspace",
                    Description = "Open patient charts, visits, and care actions.",
                    IconClass = "bi bi-clipboard2-pulse",
                    BadgeText = "Clinical",
                    BadgeClass = "badge-soft-primary",
                    LinkUrl = "/Patients",
                    LinkText = "Open Patients",
                    Footnote = "Start here for chart-based workflows."
                });
            }

            if (HasRole(normalized, "nurse"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Nursing Workspace",
                    Description = "Vitals, care plans, medication administration, and nursing notes.",
                    IconClass = "bi bi-heart-pulse",
                    BadgeText = "Nurse",
                    BadgeClass = "badge-soft-danger",
                    LinkUrl = "/Patients",
                    LinkText = "Open Patients",
                    Footnote = "Use the chart shell for bedside workflows."
                });
            }

            if (HasRole(normalized, "laboratorystaff", "labtech", "labtechnician", "laboratory", "lab"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Laboratory Workspace",
                    Description = "Requests, results, and sample follow-up.",
                    IconClass = "bi bi-heart-pulse-fill",
                    BadgeText = "Lab",
                    BadgeClass = "badge-soft-danger",
                    LinkUrl = "/Queues/Lab",
                    LinkText = "Open Queue",
                    Footnote = "See the lab worklist and status filters."
                });
            }

            if (HasRole(normalized, "pharmacist", "pharmacy"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Pharmacy Workspace",
                    Description = "Prescription review, reconciliation, and dispensing.",
                    IconClass = "bi bi-capsule-pill",
                    BadgeText = "Pharmacy",
                    BadgeClass = "badge-soft-success",
                    LinkUrl = "/Queues/Pharmacy",
                    LinkText = "Open Queue",
                    Footnote = "Keep medication fulfillment moving."
                });
            }

            if (HasRole(normalized, "receptionist", "frontdesk"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Front Desk Workspace",
                    Description = "Register patients, confirm demographics, and route visitors.",
                    IconClass = "bi bi-person-badge",
                    BadgeText = "Front Desk",
                    BadgeClass = "badge-soft-info",
                    LinkUrl = "/Patients",
                    LinkText = "Register Patients",
                    Footnote = "Patient intake begins from the patient list."
                });
            }

            if (HasRole(normalized, "billing", "cashier", "accountsofficer", "billingaccountsofficer"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Billing Workspace",
                    Description = "Invoices, balances, and collection work.",
                    IconClass = "bi bi-cash-stack",
                    BadgeText = "Billing",
                    BadgeClass = "badge-soft-warning",
                    LinkUrl = "/Queues/Billing",
                    LinkText = "Open Queue",
                    Footnote = "Review invoices waiting for payment."
                });
            }

            if (HasRole(normalized, "insurance", "claimsofficer", "insuranceclaimsofficer"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Claims Workspace",
                    Description = "Eligibility, preauthorization, and claim follow-up.",
                    IconClass = "bi bi-patch-check",
                    BadgeText = "Claims",
                    BadgeClass = "badge-soft-warning",
                    LinkUrl = "/Billing",
                    LinkText = "Open Billing",
                    Footnote = "Use billing and payments as the nearest operational entry point."
                });
            }

            if (HasRole(normalized, "medicalrecords", "him", "records"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Records Workspace",
                    Description = "Document review, corrections, and chart record access.",
                    IconClass = "bi bi-folder2-open",
                    BadgeText = "HIM",
                    BadgeClass = "badge-soft-secondary",
                    LinkUrl = "/Reports",
                    LinkText = "Open Reports",
                    Footnote = "Records and audit workflows will expand here."
                });
            }

            if (HasRole(normalized, "hr"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "HR Workspace",
                    Description = "Staff records, contracts, leave, and attendance workflows.",
                    IconClass = "bi bi-people",
                    BadgeText = "HR",
                    BadgeClass = "badge-soft-secondary",
                    LinkUrl = "/Users",
                    LinkText = "Open Users",
                    Footnote = "Use the user directory until HR screens are added."
                });
            }

            if (HasRole(normalized, "procurement"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Procurement Workspace",
                    Description = "Purchase requests, suppliers, and ordering flow.",
                    IconClass = "bi bi-truck",
                    BadgeText = "Procurement",
                    BadgeClass = "badge-soft-warning",
                    LinkUrl = "/Pharmacy/Inventory",
                    LinkText = "Open Inventory",
                    Footnote = "Inventory actions are the closest current operational entry point."
                });
            }

            if (HasRole(normalized, "inventory"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Inventory Workspace",
                    Description = "Stock, receiving, transfers, and counts.",
                    IconClass = "bi bi-box-seam",
                    BadgeText = "Store",
                    BadgeClass = "badge-soft-success",
                    LinkUrl = "/Pharmacy/Inventory",
                    LinkText = "Open Inventory",
                    Footnote = "Use pharmacy inventory until a dedicated store module is added."
                });
            }

            if (HasRole(normalized, "finance"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Finance Workspace",
                    Description = "Ledger, reconciliation, expenses, and financial review.",
                    IconClass = "bi bi-calculator",
                    BadgeText = "Finance",
                    BadgeClass = "badge-soft-success",
                    LinkUrl = "/Billing",
                    LinkText = "Open Billing",
                    Footnote = "Financial operations currently route through billing screens."
                });
            }

            if (HasRole(normalized, "hospitaloperations"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Operations Workspace",
                    Description = "Beds, wards, theatre, and operational oversight.",
                    IconClass = "bi bi-building",
                    BadgeText = "Ops",
                    BadgeClass = "badge-soft-info",
                    LinkUrl = "/Reports",
                    LinkText = "Open Reports",
                    Footnote = "Operations dashboards will grow from reports and queue views."
                });
            }

            if (HasRole(normalized, "auditor"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Audit Workspace",
                    Description = "Compliance review, governance checks, and audit trails.",
                    IconClass = "bi bi-shield-check",
                    BadgeText = "Audit",
                    BadgeClass = "badge-soft-secondary",
                    LinkUrl = "/Reports",
                    LinkText = "Open Reports",
                    Footnote = "Auditors can pivot into reports while dedicated audit pages are built."
                });
            }

            if (HasRole(normalized, "patientportal"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Patient Portal",
                    Description = "Your personal profile, records, billing, and results.",
                    IconClass = "bi bi-person-circle",
                    BadgeText = "Portal",
                    BadgeClass = "badge-soft-primary",
                    LinkUrl = "/Profile/Me",
                    LinkText = "Open Profile",
                    Footnote = "Patient-facing journeys should begin from the profile screen."
                });
            }

            if (HasRole(normalized, "systemadministrator", "hospitaladministrator", "admin", "superadmin", "hospitaladmin"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Administration",
                    Description = "Users, roles, permissions, and reports.",
                    IconClass = "bi bi-shield-lock",
                    BadgeText = "Admin",
                    BadgeClass = "badge-soft-secondary",
                    LinkUrl = "/Users",
                    LinkText = "Open Users",
                    Footnote = "Pair with role management and reporting."
                });
            }

            if (!cards.Any())
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "General Workspace",
                    Description = "Browse patients and move into the chart shell.",
                    IconClass = "bi bi-people-fill",
                    BadgeText = "General",
                    BadgeClass = "badge-soft-primary",
                    LinkUrl = "/Patients",
                    LinkText = "Browse Patients",
                    Footnote = "Use module shortcuts from the sidebar when needed."
                });
            }

            return cards.ToArray();
        }

        private static HMS.UI.Models.DashboardCardViewModel[] BuildQuickActions(string[] roles)
        {
            var cards = new System.Collections.Generic.List<HMS.UI.Models.DashboardCardViewModel>
            {
                new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Patient Search",
                    Description = "Find a patient quickly and open the chart shell.",
                    IconClass = "bi bi-search",
                    BadgeText = "Core",
                    BadgeClass = "badge-soft-primary",
                    LinkUrl = "/Patients",
                    LinkText = "Search Patients"
                },
                new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Queues",
                    Description = "Jump into the role-based work queue hub.",
                    IconClass = "bi bi-list-task",
                    BadgeText = "Work",
                    BadgeClass = "badge-soft-info",
                    LinkUrl = "/Queues",
                    LinkText = "Open Queues"
                }
            };

            var normalized = roles.Select(NormalizeRoleKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (HasRole(normalized, "systemadministrator", "hospitaladministrator", "admin", "superadmin", "hospitaladmin"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Roles & Permissions",
                    Description = "Manage access policies and user groups.",
                    IconClass = "bi bi-person-gear",
                    BadgeText = "Admin",
                    BadgeClass = "badge-soft-secondary",
                    LinkUrl = "/Roles",
                    LinkText = "Open Roles"
                });
            }

            return cards.ToArray();
        }

        private static HMS.UI.Models.DashboardCardViewModel[] BuildQueueCards(string[] roles, System.Collections.Generic.Dictionary<string, int> counts)
        {
            var normalized = roles.Select(NormalizeRoleKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var cards = new System.Collections.Generic.List<HMS.UI.Models.DashboardCardViewModel>();

            if (HasRole(normalized, "doctor", "physician", "clinical"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Clinical Queue",
                    Description = "Patient search and chart launch point.",
                    IconClass = "bi bi-journal-medical",
                    BadgeText = "Patients",
                    BadgeClass = "badge-soft-primary",
                    LinkUrl = "/Patients",
                    LinkText = "Open",
                    Footnote = "Use the patient list to enter the chart shell."
                });
            }

            if (HasRole(normalized, "nurse"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Nursing Queue",
                    Description = "Open patients needing nursing assessment and follow-up.",
                    IconClass = "bi bi-heart-pulse",
                    BadgeText = "Nurse",
                    BadgeClass = "badge-soft-danger",
                    LinkUrl = "/Patients",
                    LinkText = "Open Patients",
                    Footnote = "Use the chart shell for bedside charting."
                });
            }

            if (HasRole(normalized, "laboratorystaff", "labtech", "labtechnician", "laboratory", "lab"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Lab Queue",
                    Description = "Pending lab requests awaiting review.",
                    IconClass = "bi bi-heart-pulse-fill",
                    BadgeText = "Lab",
                    BadgeClass = "badge-soft-danger",
                    LinkUrl = "/Queues/Lab",
                    LinkText = "Open Queue",
                    CountText = counts.TryGetValue("lab", out var labCount) ? labCount.ToString() : "0",
                    Footnote = "Status filters are available on the queue page."
                });
            }

            if (HasRole(normalized, "pharmacist", "pharmacy"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Pharmacy Queue",
                    Description = "Prescriptions awaiting fulfillment.",
                    IconClass = "bi bi-capsule-pill",
                    BadgeText = "Pharmacy",
                    BadgeClass = "badge-soft-success",
                    LinkUrl = "/Queues/Pharmacy",
                    LinkText = "Open Queue",
                    CountText = counts.TryGetValue("pharmacy", out var rxCount) ? rxCount.ToString() : "0",
                    Footnote = "Keep dispensing tasks moving."
                });
            }

            if (HasRole(normalized, "receptionist", "frontdesk"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Front Desk Queue",
                    Description = "Patient registration and intake work.",
                    IconClass = "bi bi-person-badge",
                    BadgeText = "Front Desk",
                    BadgeClass = "badge-soft-info",
                    LinkUrl = "/Patients",
                    LinkText = "Open Patients",
                    Footnote = "Registration begins from the patient list."
                });
            }

            if (HasRole(normalized, "billing", "cashier", "accountsofficer", "billingaccountsofficer"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Billing Queue",
                    Description = "Unpaid invoices and outstanding balances.",
                    IconClass = "bi bi-cash-stack",
                    BadgeText = "Billing",
                    BadgeClass = "badge-soft-warning",
                    LinkUrl = "/Queues/Billing",
                    LinkText = "Open Queue",
                    CountText = counts.TryGetValue("billing", out var invCount) ? invCount.ToString() : "0",
                    Footnote = "Review unpaid invoices and balances."
                });
            }

            if (HasRole(normalized, "insurance", "claimsofficer", "insuranceclaimsofficer"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Claims Queue",
                    Description = "Eligibility and claim follow-up tasks.",
                    IconClass = "bi bi-patch-check",
                    BadgeText = "Claims",
                    BadgeClass = "badge-soft-warning",
                    LinkUrl = "/Billing",
                    LinkText = "Open Billing",
                    Footnote = "Claim handling will expand from billing workflows."
                });
            }

            if (HasRole(normalized, "medicalrecords", "him", "records"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Records Queue",
                    Description = "Documents, corrections, and chart review tasks.",
                    IconClass = "bi bi-folder2-open",
                    BadgeText = "HIM",
                    BadgeClass = "badge-soft-secondary",
                    LinkUrl = "/Reports",
                    LinkText = "Open Reports",
                    Footnote = "Dedicated HIM pages can slot in later."
                });
            }

            if (HasRole(normalized, "hr"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "HR Queue",
                    Description = "Staff and attendance administration tasks.",
                    IconClass = "bi bi-people",
                    BadgeText = "HR",
                    BadgeClass = "badge-soft-secondary",
                    LinkUrl = "/Users",
                    LinkText = "Open Users",
                    Footnote = "Use the user directory as the current entry point."
                });
            }

            if (HasRole(normalized, "procurement"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Procurement Queue",
                    Description = "Ordering and supplier follow-up work.",
                    IconClass = "bi bi-truck",
                    BadgeText = "Procure",
                    BadgeClass = "badge-soft-warning",
                    LinkUrl = "/Pharmacy/Inventory",
                    LinkText = "Open Inventory",
                    Footnote = "Use inventory screens until procurement UI exists."
                });
            }

            if (HasRole(normalized, "inventory"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Inventory Queue",
                    Description = "Stock levels, receiving, transfers, and counts.",
                    IconClass = "bi bi-box-seam",
                    BadgeText = "Store",
                    BadgeClass = "badge-soft-success",
                    LinkUrl = "/Pharmacy/Inventory",
                    LinkText = "Open Inventory",
                    Footnote = "Pharmacy inventory is the current operational bridge."
                });
            }

            if (HasRole(normalized, "finance"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Finance Queue",
                    Description = "Collections, reconciliations, and financial review tasks.",
                    IconClass = "bi bi-calculator",
                    BadgeText = "Finance",
                    BadgeClass = "badge-soft-success",
                    LinkUrl = "/Billing",
                    LinkText = "Open Billing",
                    Footnote = "Billing screens currently serve as the finance entry point."
                });
            }

            if (HasRole(normalized, "hospitaloperations"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Operations Queue",
                    Description = "Beds, wards, theatre, and operational KPIs.",
                    IconClass = "bi bi-building",
                    BadgeText = "Ops",
                    BadgeClass = "badge-soft-info",
                    LinkUrl = "/Reports",
                    LinkText = "Open Reports",
                    Footnote = "Operations views will grow alongside the workflow modules."
                });
            }

            if (HasRole(normalized, "auditor"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Audit Queue",
                    Description = "Review trails, reports, and compliance exceptions.",
                    IconClass = "bi bi-shield-check",
                    BadgeText = "Audit",
                    BadgeClass = "badge-soft-secondary",
                    LinkUrl = "/Reports",
                    LinkText = "Open Reports",
                    Footnote = "Use reports for now while dedicated audit pages are added."
                });
            }

            if (HasRole(normalized, "systemadministrator", "hospitaladministrator", "admin", "superadmin", "hospitaladmin"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Admin Overview",
                    Description = "User and security administration work.",
                    IconClass = "bi bi-shield-lock",
                    BadgeText = "Admin",
                    BadgeClass = "badge-soft-secondary",
                    LinkUrl = "/Users",
                    LinkText = "Open Users",
                    CountText = counts.TryGetValue("debt", out var debtCount) ? debtCount.ToString() : "0",
                    Footnote = "Audit and governance tasks live alongside the queues."
                });
            }

            if (HasRole(normalized, "patientportal"))
            {
                cards.Add(new HMS.UI.Models.DashboardCardViewModel
                {
                    Title = "Patient Area",
                    Description = "Manage your profile and access patient-facing services.",
                    IconClass = "bi bi-person-circle",
                    BadgeText = "Portal",
                    BadgeClass = "badge-soft-primary",
                    LinkUrl = "/Profile/Me",
                    LinkText = "Open Profile",
                    Footnote = "This is the patient-facing starting point."
                });
            }

            return cards.ToArray();
        }

        private static string NormalizeRole(string value)
        {
            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        private static bool HasRole(System.Collections.Generic.ISet<string> normalizedRoles, params string[] roleNames)
        {
            return roleNames.Select(NormalizeRoleKey).Any(normalizedRoles.Contains);
        }

        private static string NormalizeRoleKey(string value)
        {
            var normalized = NormalizeRole(value);
            return normalized switch
            {
                "systemadministrator" or "admin" => "systemadministrator",
                "hospitaladministratorsuperadmin" or "hospitaladministrator" or "superadmin" or "hospitaladmin" => "hospitaladministrator",
                "doctorphysician" or "doctor" or "physician" => "doctor",
                "nurse" => "nurse",
                "pharmacist" => "pharmacist",
                "laboratorystaff" or "labtech" or "labtechnician" or "laboratory" or "lab" => "laboratory",
                "radiologystaff" or "radiology" => "radiology",
                "receptionistfrontdesk" or "receptionist" or "frontdesk" => "receptionist",
                "billingaccountsofficer" or "billing" or "accountsofficer" => "billing",
                "insuranceclaimsofficer" or "insurance" or "claims" or "claimsofficer" => "insurance",
                "cashier" => "cashier",
                "medicalrecordshimofficer" or "medicalrecords" or "him" or "himofficer" or "records" => "medicalrecords",
                "hrstaffmanager" or "hr" or "staffmanager" => "hr",
                "procurementofficer" or "procurement" => "procurement",
                "inventorystoremanager" or "inventory" or "storemanager" => "inventory",
                "financemanageraccountant" or "finance" or "accountant" => "finance",
                "departmentmanagerheadofdepartment" or "departmentmanager" or "headofdepartment" or "hod" => "departmentmanager",
                "hospitaloperationsmanager" or "operationsmanager" or "operations" => "hospitaloperations",
                "auditorcomplianceofficer" or "auditor" or "complianceofficer" => "auditor",
                "patientportaluser" or "patientportal" or "patient" or "user" => "patientportal",
                _ => normalized
            };
        }
    }
}
