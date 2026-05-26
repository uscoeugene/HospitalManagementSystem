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

        public async Task<IActionResult> Index(int page = 1, int pageSize = 50, string? search = null)
        {
            try
            {
                // call API with paging params (API already supports tenancy-aware listing)
                var q = $"/auth/users?page={page}&pageSize={pageSize}" + (string.IsNullOrWhiteSpace(search) ? string.Empty : "&search=" + System.Uri.EscapeDataString(search));
                var raw = await _api.GetAsync<System.Text.Json.JsonElement>(q);

                var vm = new HMS.UI.Models.Users.UserListViewModel { Page = page, PageSize = pageSize, Search = search };

                if (raw.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (raw.TryGetProperty("total", out var totalP) && totalP.ValueKind == System.Text.Json.JsonValueKind.Number) vm.TotalCount = totalP.GetInt32();
                    if (raw.TryGetProperty("items", out var items) && items.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var it in items.EnumerateArray())
                        {
                            var u = MapUserFromJsonElement(it);
                            if (u != null)
                            {
                                // try include photoUrl/lastLogin if present
                                if (it.TryGetProperty("photoUrl", out var pu) && pu.ValueKind == System.Text.Json.JsonValueKind.String) u.PhotoUrl = _api.MakeAbsoluteUrl(pu.GetString());
                                if (it.TryGetProperty("lastLogin", out var ll) && (ll.ValueKind == System.Text.Json.JsonValueKind.String || ll.ValueKind == System.Text.Json.JsonValueKind.Number))
                                {
                                    try { u.LastLogin = DateTimeOffset.Parse(ll.GetString()); } catch { }
                                }
                                vm.Items.Add(u);
                            }
                        }
                    }
                }
                else if (raw.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var it in raw.EnumerateArray())
                    {
                        var u = MapUserFromJsonElement(it);
                        if (u != null)
                        {
                            if (it.TryGetProperty("photoUrl", out var pu) && pu.ValueKind == System.Text.Json.JsonValueKind.String) u.PhotoUrl = _api.MakeAbsoluteUrl(pu.GetString());
                            vm.Items.Add(u);
                        }
                    }
                }

                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(new HMS.UI.Models.Users.UserListViewModel());
            }
        }

        public async Task<IActionResult> Details(Guid id)
        {
            try
            {
                var raw = await _api.GetAsync<System.Text.Json.JsonElement>($"/auth/users/{id}");
                var u = MapUserFromJsonElement(raw);
                if (u == null) u = new HMS.UI.Models.Users.UserListItemViewModel { Id = id };

                // fetch profile to get photo and full name
                try
                {
                    var profile = await _api.GetAsync<HMS.UI.Models.Profile.UserProfileViewModel>($"/api/Profile/{u.Id}");
                    if (profile != null)
                    {
                        u.PhotoUrl = string.IsNullOrWhiteSpace(profile.PhotoUrl) ? u.PhotoUrl : _api.MakeAbsoluteUrl(profile.PhotoUrl);
                        u.FullName = string.Join(' ', new[] { profile.FirstName, profile.OtherNames, profile.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    }
                }
                catch { }

                // fetch tenant name if available
                try
                {
                    if (u.TenantId.HasValue)
                    {
                        var t = await _api.GetAsync<System.Text.Json.JsonElement>($"/tenants/{u.TenantId.Value}");
                        if (t.ValueKind == System.Text.Json.JsonValueKind.Object && t.TryGetProperty("name", out var n) && n.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            u.TenantName = n.GetString();
                        }
                    }
                }
                catch { }

                // fetch roles server-side (returns role ids or names depending on API)
                try
                {
                    var roleIdsOrNames = await _api.GetAsync<System.Text.Json.JsonElement>($"/auth/users/{id}/roles");
                    if (roleIdsOrNames.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var list = new System.Collections.Generic.List<string>();
                        var roleIds = new System.Collections.Generic.List<Guid>();
                        foreach (var r in roleIdsOrNames.EnumerateArray())
                        {
                            if (r.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                var sval = r.GetString() ?? string.Empty;
                                // try parse as guid
                                if (Guid.TryParse(sval, out var rg)) roleIds.Add(rg);
                                else list.Add(sval);
                            }
                            else if (r.ValueKind == System.Text.Json.JsonValueKind.Object && r.TryGetProperty("name", out var rn) && rn.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                list.Add(rn.GetString() ?? string.Empty);
                            }
                        }

                        // If we have role ids, fetch role names from /roles and map
                        if (roleIds.Any())
                        {
                            try
                            {
                                var allRoles = await _api.GetAsync<HMS.UI.Models.Users.RoleViewModel[]>("/roles");
                                if (allRoles != null)
                                {
                                    foreach (var rid in roleIds)
                                    {
                                        var r = allRoles.FirstOrDefault(x => x.Id == rid);
                                        if (r != null) list.Add(r.Name);
                                        else list.Add(rid.ToString());
                                    }
                                }
                                else
                                {
                                    foreach (var rid in roleIds) list.Add(rid.ToString());
                                }
                            }
                            catch
                            {
                                foreach (var rid in roleIds) list.Add(rid.ToString());
                            }
                        }

                        u.Roles = list.ToArray();
                    }
                    else if (roleIdsOrNames.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        // fallback
                    }
                }
                catch { }

                // fetch recent activity/audit server-side
                try
                {
                    var audits = await _api.GetAsync<System.Text.Json.JsonElement>($"/auth/audits?userId={id}&pageSize=20");
                    var activities = new System.Collections.Generic.List<HMS.UI.Models.Users.AuditEntryViewModel>();
                    if (audits.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var a in audits.EnumerateArray())
                        {
                            try
                            {
                                var act = new HMS.UI.Models.Users.AuditEntryViewModel();
                                if (a.TryGetProperty("performedAt", out var pa) && pa.ValueKind == System.Text.Json.JsonValueKind.String) act.PerformedAt = DateTimeOffset.Parse(pa.GetString());
                                else if (a.TryGetProperty("PerformedAt", out var pa2) && pa2.ValueKind == System.Text.Json.JsonValueKind.String) act.PerformedAt = DateTimeOffset.Parse(pa2.GetString());
                                if (a.TryGetProperty("action", out var ac) && ac.ValueKind == System.Text.Json.JsonValueKind.String) act.Action = ac.GetString() ?? string.Empty;
                                else if (a.TryGetProperty("Action", out var ac2) && ac2.ValueKind == System.Text.Json.JsonValueKind.String) act.Action = ac2.GetString() ?? string.Empty;
                                if (a.TryGetProperty("details", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.String) act.Details = d.GetString();
                                activities.Add(act);
                            }
                            catch { }
                        }
                    }
                    u.Activity = activities.ToArray();
                }
                catch { }

                return View(u);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminAction(Guid id, string action, string? newPassword = null)
        {
            try
            {
                if (string.Equals(action, "lock", StringComparison.OrdinalIgnoreCase))
                {
                    await _api.PostAsync<object>($"/auth/users/{id}/lock", new { });
                    TempData["Success"] = "User locked";
                }
                else if (string.Equals(action, "unlock", StringComparison.OrdinalIgnoreCase))
                {
                    await _api.PostAsync<object>($"/auth/users/{id}/unlock", new { });
                    TempData["Success"] = "User unlocked";
                }
                else if (string.Equals(action, "reset", StringComparison.OrdinalIgnoreCase))
                {
                    // Use provided newPassword if supplied (from admin modal) otherwise generate one
                    var temp = newPassword;
                    if (string.IsNullOrWhiteSpace(temp))
                    {
                        temp = "TempPass123!"; // fallback
                    }
                    await _api.PostAsync<object>($"/auth/users/{id}/reset-password", new { NewPasswordPlain = temp });
                    TempData["Success"] = "Password reset";
                }

                return RedirectToAction("Edit", new { id = id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Edit", new { id = id });
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

                // build strongly-typed view model for the view
                var vm = new HMS.UI.Models.Users.UserEditViewModel();
                vm.Id = user?.Id ?? id;
                vm.Username = user?.Username ?? string.Empty;
                vm.Email = user?.Email;
                vm.Roles = roles.ToArray();
                vm.AssignedRoleIds = assigned ?? Array.Empty<Guid>();

                // populate auxiliary display names
                try
                {
                    if (user != null)
                    {
                        // full name from profile
                        var prof = await _api.GetAsync<HMS.UI.Models.Profile.UserProfileViewModel>($"/api/Profile/{user.Id}");
                        if (prof != null)
                        {
                            vm.FirstName = prof.FirstName;
                            vm.LastName = prof.LastName;
                            if (!string.IsNullOrWhiteSpace(prof.PhotoUrl)) vm.PhotoUrl = _api.MakeAbsoluteUrl(prof.PhotoUrl);
                        }

                        if (user.TenantId.HasValue)
                        {
                            var t = await _api.GetAsync<System.Text.Json.JsonElement>($"/tenants/{user.TenantId.Value}");
                            if (t.ValueKind == System.Text.Json.JsonValueKind.Object && t.TryGetProperty("name", out var n) && n.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                vm.TenantName = n.GetString();
                            }
                        }
                    }
                }
                catch { }

                // Also fetch assigned role names for display
                try
                {
                    var assignedNames = new System.Collections.Generic.List<string>();
                    foreach (var rid in assigned ?? Array.Empty<Guid>())
                    {
                        var rObj = roles.FirstOrDefault(r => r.Id == rid);
                        if (rObj != null) assignedNames.Add(rObj.Name);
                    }
                    ViewBag.AssignedNames = assignedNames.ToArray();
                }
                catch { }

                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Guid[] roles, Microsoft.AspNetCore.Http.IFormFile? photo, [FromForm] string? firstName = null, [FromForm] string? lastName = null, [FromForm] string? email = null, [FromForm] string? newPassword = null)
        {
            try
            {
                // Roles: remove those not present and add new ones
                var existing = await _api.GetAsync<Guid[]>($"/auth/users/{id}/roles");
                var toRemove = existing?.Except(roles) ?? Array.Empty<Guid>();
                var toAdd = roles.Except(existing ?? Array.Empty<Guid>());

                foreach (var r in toRemove)
                {
                    try { await _api.DeleteRawAsync($"/auth/users/{id}/roles/{r}"); } catch { }
                }

                foreach (var r in toAdd)
                {
                    try { await _api.PostRawAsync($"/auth/users/{id}/roles/{r}", null); } catch { }
                }

                // Password reset if provided
                if (!string.IsNullOrWhiteSpace(newPassword))
                {
                    try
                    {
                        await _api.PostAsync<object>($"/auth/users/{id}/reset-password", new { NewPasswordPlain = newPassword });
                        TempData["Success"] = "Password reset";
                    }
                    catch (Exception ex)
                    {
                        TempData["Error"] = "Password reset failed: " + ex.Message;
                        return RedirectToAction("Edit", new { id = id });
                    }
                }

                // Profile update
                try
                {
                    var profilePayload = new System.Collections.Generic.Dictionary<string, object?>();
                    if (!string.IsNullOrWhiteSpace(firstName)) profilePayload["FirstName"] = firstName;
                    if (!string.IsNullOrWhiteSpace(lastName)) profilePayload["LastName"] = lastName;
                    if (!string.IsNullOrWhiteSpace(email)) profilePayload["Email"] = email;

                    if (photo != null && photo.Length > 0)
                    {
                        // Upload photo to API (admin endpoint)
                        try
                        {
                            var resp = await _api.PostFileAsync($"/api/profile/{id}/photo", photo);
                            if (resp.IsSuccessStatusCode)
                            {
                                var body = await resp.Content.ReadAsStringAsync();
                                try
                                {
                                    using var jd = System.Text.Json.JsonDocument.Parse(body);
                                    if (jd.RootElement.TryGetProperty("url", out var u))
                                    {
                                        var rel = u.GetString();
                                        if (!string.IsNullOrWhiteSpace(rel)) profilePayload["PhotoUrl"] = rel;
                                    }
                                }
                                catch { }
                            }
                            else
                            {
                                var b = await resp.Content.ReadAsStringAsync();
                                TempData["Error"] = "Photo upload failed: " + b;
                                return RedirectToAction("Edit", new { id = id });
                            }
                        }
                        catch (Exception ex)
                        {
                            TempData["Error"] = "Photo upload failed: " + ex.Message;
                            return RedirectToAction("Edit", new { id = id });
                        }
                    }

                    if (profilePayload.Any())
                    {
                        var raw = await _api.PutRawAsync($"/api/profile/{id}", profilePayload);
                        if (!raw.IsSuccessStatusCode)
                        {
                            var b = await raw.Content.ReadAsStringAsync();
                            TempData["Error"] = "Profile update failed: " + b;
                            return RedirectToAction("Edit", new { id = id });
                        }
                    }
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Profile update failed: " + ex.Message;
                    return RedirectToAction("Edit", new { id = id });
                }

                TempData["Success"] = (TempData["Success"] as string) ?? "User updated";
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
