using System;
using System.Threading.Tasks;
using HMS.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace HMS.UI.Controllers
{
    [HMS.UI.Security.HasPermission("users.manage")]
    public class UsersController : Controller
    {
        private readonly ApiClient _api;

        public UsersController(ApiClient api)
        {
            _api = api;
        }

        private static HMS.UI.Models.Users.UserListItemViewModel? MapUserFromJsonElement(System.Text.Json.JsonElement el)
        {
            try
            {
                var u = new HMS.UI.Models.Users.UserListItemViewModel();
                if (el.TryGetProperty("id", out var idp) && idp.ValueKind == System.Text.Json.JsonValueKind.String && Guid.TryParse(idp.GetString(), out var gid)) u.Id = gid;
                else if (el.TryGetProperty("Id", out var idp2) && idp2.ValueKind == System.Text.Json.JsonValueKind.String && Guid.TryParse(idp2.GetString(), out var gid2)) u.Id = gid2;

                if (el.TryGetProperty("username", out var up) && up.ValueKind == System.Text.Json.JsonValueKind.String) u.Username = up.GetString() ?? string.Empty;
                else if (el.TryGetProperty("Username", out var up2) && up2.ValueKind == System.Text.Json.JsonValueKind.String) u.Username = up2.GetString() ?? string.Empty;

                if (el.TryGetProperty("email", out var ep) && ep.ValueKind == System.Text.Json.JsonValueKind.String) u.Email = ep.GetString();
                else if (el.TryGetProperty("Email", out var ep2) && ep2.ValueKind == System.Text.Json.JsonValueKind.String) u.Email = ep2.GetString();

                if (el.TryGetProperty("tenantId", out var tp) && tp.ValueKind == System.Text.Json.JsonValueKind.String && Guid.TryParse(tp.GetString(), out var tid)) u.TenantId = tid;
                else if (el.TryGetProperty("TenantId", out var tp2) && tp2.ValueKind == System.Text.Json.JsonValueKind.String && Guid.TryParse(tp2.GetString(), out var tid2)) u.TenantId = tid2;

                if (el.TryGetProperty("isLocked", out var lk) && lk.ValueKind == System.Text.Json.JsonValueKind.True) u.IsLocked = true;
                else if (el.TryGetProperty("IsLocked", out var lk2) && lk2.ValueKind == System.Text.Json.JsonValueKind.True) u.IsLocked = true;

                return u;
            }
            catch
            {
                return null;
            }
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var raw = await _api.GetAsync<System.Text.Json.JsonElement>("/auth/users");
                var list = new System.Collections.Generic.List<HMS.UI.Models.Users.UserListItemViewModel>();

                if (raw.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    // maybe wrapped as { total, page, items: [...] }
                    if (raw.TryGetProperty("items", out var items) && items.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var it in items.EnumerateArray())
                        {
                            var u = MapUserFromJsonElement(it);
                            if (u != null) list.Add(u);
                        }
                    }
                }
                else if (raw.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var it in raw.EnumerateArray())
                    {
                        var u = MapUserFromJsonElement(it);
                        if (u != null) list.Add(u);
                    }
                }

                return View(list);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(new System.Collections.Generic.List<HMS.UI.Models.Users.UserListItemViewModel>());
            }
        }

        public IActionResult Create()
        {
            try
            {
                // if UI running in tenant context, the tenant cookie will be present and we don't need to show tenant selector
                var tenantCookie = Request.Cookies["HmsTenantId"] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(tenantCookie))
                {
                    // load tenants so super-admin can choose
                    var tenants = _api.GetAsync<HMS.UI.Models.TenantItem[]>("/tenants").GetAwaiter().GetResult();
                    ViewBag.Tenants = tenants ?? Array.Empty<HMS.UI.Models.TenantItem>();
                }

                // load available roles for assignment
                var rolesResp = _api.GetAsync<HMS.UI.Models.Users.RoleViewModel[]>("/roles").GetAwaiter().GetResult();
                ViewBag.Roles = rolesResp ?? Array.Empty<HMS.UI.Models.Users.RoleViewModel>();

                ViewBag.CurrentTenantId = string.IsNullOrWhiteSpace(tenantCookie) ? (Guid?)null : (Guid.TryParse(tenantCookie, out var tid) ? tid : (Guid?)null);
            }
            catch
            {
                ViewBag.Tenants = Array.Empty<HMS.UI.Models.TenantItem>();
                ViewBag.Roles = Array.Empty<HMS.UI.Models.Users.RoleViewModel>();
            }

            return View();
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            try
            {
                HMS.UI.Models.Users.UserListItemViewModel? user = null;
                try
                {
                    var rawUser = await _api.GetAsync<System.Text.Json.JsonElement>($"/auth/users/{id}");
                    if (rawUser.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        user = MapUserFromJsonElement(rawUser);
                    }
                }
                catch
                {
                    user = null;
                }

                // roles endpoint returns RoleResponse[]
                var rolesResp = await _api.GetAsync<HMS.UI.Models.Users.RoleViewModel[]>("/roles");
                var roles = new System.Collections.Generic.List<HMS.UI.Models.Users.RoleViewModel>();
                if (rolesResp != null)
                {
                    roles.AddRange(rolesResp);
                }

                // fetch assigned role ids
                var assigned = await _api.GetAsync<Guid[]>($"/auth/users/{id}/roles");

                ViewBag.User = user;
                ViewBag.Roles = roles.ToArray();
                ViewBag.Assigned = assigned ?? Array.Empty<Guid>();

                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Guid[] roles)
        {
            // assign roles: simplistic approach - remove all then add provided
            try
            {
                var existing = await _api.GetAsync<Guid[]>($"/auth/users/{id}/roles");
                var toRemove = existing?.Except(roles) ?? Array.Empty<Guid>();
                var toAdd = roles.Except(existing ?? Array.Empty<Guid>());

                foreach (var r in toRemove)
                {
                    await _api.DeleteRawAsync($"/auth/users/{id}/roles/{r}");
                }

                foreach (var r in toAdd)
                {
                    await _api.PostRawAsync($"/auth/users/{id}/roles/{r}", null);
                }

                TempData["Success"] = "Roles updated";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Edit", new { id = id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] string username, [FromForm] string password, [FromForm] string email,
            [FromForm] string firstName = "", [FromForm] string lastName = "", [FromForm] Guid? tenantId = null, [FromForm] Guid[] roles = null)
        {
            try
            {
                var payload = new { Username = username, Password = password, Email = email, FirstName = string.IsNullOrWhiteSpace(firstName) ? null : firstName, LastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName };

                // Determine tenant - prefer cookie if present (tenant admin creating user)
                Guid? tenantToUse = null;
                try
                {
                    var cookie = Request.Cookies["HmsTenantId"] ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(cookie) && Guid.TryParse(cookie, out var cid)) tenantToUse = cid;
                }
                catch { }

                if (tenantToUse == null && tenantId.HasValue) tenantToUse = tenantId;

                // Use central auth register endpoint so users are created in main auth tables (tenant resolved by middleware/header)
                var path = "/auth/register"; // tenant context resolved by middleware or X-Tenant-Id header forwarded by ApiClient
                System.Collections.Generic.IDictionary<string, string>? headers = null;
                if (tenantToUse.HasValue)
                {
                    headers = new System.Collections.Generic.Dictionary<string, string> { ["X-Tenant-Id"] = tenantToUse.Value.ToString() };
                }

                // Use PostRawAsync when we need to send custom headers (tenant selection).
                // Do NOT call TrySetAuthCookieFromResponseAsync here: creating a user must not replace the current admin's auth cookies.
                var respMsg = await _api.PostRawAsync(path, payload, headers);

                // If the API returned non-success status, surface the error to the UI immediately.
                if (!respMsg.IsSuccessStatusCode)
                {
                    var err = await respMsg.Content.ReadAsStringAsync();
                    // Try extract useful message
                    try
                    {
                        using var ed = System.Text.Json.JsonDocument.Parse(err);
                        if (ed.RootElement.TryGetProperty("detail", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.String) err = d.GetString() ?? err;
                        else if (ed.RootElement.TryGetProperty("error", out var e) && e.ValueKind == System.Text.Json.JsonValueKind.String) err = e.GetString() ?? err;
                    }
                    catch { }

                    ModelState.AddModelError(string.Empty, "User creation failed: " + err);
                    // reload roles/tenants for the form
                    try
                    {
                        var tenants = await _api.GetAsync<HMS.UI.Models.TenantItem[]>("/tenants");
                        ViewBag.Tenants = tenants ?? Array.Empty<HMS.UI.Models.TenantItem>();
                        var rolesResp2 = await _api.GetAsync<HMS.UI.Models.Users.RoleViewModel[]>("/roles");
                        ViewBag.Roles = rolesResp2 ?? Array.Empty<HMS.UI.Models.Users.RoleViewModel>();
                    }
                    catch { }

                    return View();
                }

                // after create, try to obtain created user id from response body (register returns LoginResponse with UserId)
                HMS.UI.Models.Users.UserListItemViewModel? created = null;
                try
                {
                    var respBody = await respMsg.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(respBody))
                    {
                        using var jd = System.Text.Json.JsonDocument.Parse(respBody);
                        var root = jd.RootElement;
                        // unwrap ApiResponse shape
                        if (root.ValueKind == System.Text.Json.JsonValueKind.Object && root.TryGetProperty("data", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            root = d;
                        }

                        // UserId may be present
                        if (root.TryGetProperty("userId", out var uid) && uid.ValueKind == System.Text.Json.JsonValueKind.String && Guid.TryParse(uid.GetString(), out var g))
                        {
                            created = new HMS.UI.Models.Users.UserListItemViewModel { Id = g, Username = username };
                        }
                        else if (root.TryGetProperty("userId", out var uid2) && uid2.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            // ignore numeric ids
                        }
                    }
                }
                catch { }

                // If response didn't include created user id, fall back to searching users by username
                if (created == null)
                {
                    try
                    {
                        // search central users endpoint
                        var raw = await _api.GetAsync<System.Text.Json.JsonElement>($"/auth/users?search={System.Uri.EscapeDataString(username)}&pageSize=50");
                        if (raw.ValueKind == System.Text.Json.JsonValueKind.Object && raw.TryGetProperty("items", out var items) && items.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var it in items.EnumerateArray())
                            {
                                var u = MapUserFromJsonElement(it);
                                if (u != null && string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)) { created = u; break; }
                            }
                        }
                        else if (raw.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var it in raw.EnumerateArray())
                            {
                                var u = MapUserFromJsonElement(it);
                                if (u != null && string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)) { created = u; break; }
                            }
                        }
                    }
                    catch { }
                }

                if (created != null)
                {
                    // assign provided roles (or at least default User role)
                    var roleIds = roles ?? Array.Empty<Guid>();
                    if (!roleIds.Any())
                    {
                        // fetch roles and pick 'User' if present
                        var all = await _api.GetAsync<HMS.UI.Models.Users.RoleViewModel[]>("/roles");
                        if (all != null)
                        {
                            var userRole = all.FirstOrDefault(r => string.Equals(r.Name, "User", StringComparison.OrdinalIgnoreCase));
                            if (userRole != null) roleIds = new Guid[] { userRole.Id };
                        }
                    }

                    // propagate tenant header when present so the auth service assigns roles in tenant context
                    System.Collections.Generic.IDictionary<string, string>? roleHeaders = null;
                    if (tenantToUse.HasValue)
                    {
                        roleHeaders = new System.Collections.Generic.Dictionary<string, string> { ["X-Tenant-Id"] = tenantToUse.Value.ToString() };
                    }

                    foreach (var r in roleIds)
                    {
                        try { await _api.PostRawAsync($"/auth/users/{created.Id}/roles/{r}", null, roleHeaders); } catch { }
                    }

                    // create basic profile with firstname/lastname (use API Profile endpoint)
                    try
                    {
                        var profilePayload = new { FirstName = string.IsNullOrWhiteSpace(firstName) ? null : firstName, LastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName };
                        // ensure profile creation uses tenant header when appropriate
                        var putHeaders = (tenantToUse.HasValue) ? new System.Collections.Generic.Dictionary<string, string> { ["X-Tenant-Id"] = tenantToUse.Value.ToString() } : null;
                        var pResp = await _api.PutRawAsync($"/api/profile/{created.Id}", profilePayload, putHeaders);
                        if (!pResp.IsSuccessStatusCode)
                        {
                            var body = await pResp.Content.ReadAsStringAsync();
                            TempData["Error"] = "Profile creation failed: " + body;
                            return View();
                        }
                    }
                    catch (Exception ex)
                    {
                        TempData["Error"] = "Profile creation error: " + ex.Message;
                        return View();
                    }
                }

                TempData["Success"] = "User created";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View();
            }
        }
    }
}
