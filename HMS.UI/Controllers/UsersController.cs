using HMS.UI.Models;
using HMS.UI.Models.Users;
using HMS.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace HMS.UI.Controllers;

[HMS.UI.Security.HasPermission("users.manage")]
public class UsersController : Controller
{
    private readonly ApiClient _api;

    public UsersController(ApiClient api)
    {
        _api = api;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 20, string? search = null)
    {
        try
        {
            var vm = await _api.GetAsync<UserListViewModel>($"/auth/users?page={page}&pageSize={pageSize}" +
                (string.IsNullOrWhiteSpace(search) ? string.Empty : $"&search={Uri.EscapeDataString(search)}"));

            return View(vm ?? new UserListViewModel { Page = page, PageSize = pageSize, Search = search });
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return View(new UserListViewModel { Page = page, PageSize = pageSize, Search = search });
        }
    }

    public async Task<IActionResult> Details(Guid id)
    {
        try
        {
            var user = await _api.GetAsync<UserListItemViewModel>($"/auth/users/{id}");
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(user);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    public async Task<IActionResult> Create()
    {
        await PopulateCreateScreenDataAsync();
        return View(new UserEditViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserEditViewModel model, string password)
    {
        if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(password))
        {
            TempData["Error"] = "Username, email, password, and at least one role are required.";
            await PopulateCreateScreenDataAsync();
            return View(model);
        }

        if (model.AssignedRoleIds == null || model.AssignedRoleIds.Length == 0)
        {
            TempData["Error"] = "Assign at least one role before creating the user.";
            await PopulateCreateScreenDataAsync();
            return View(model);
        }

        try
        {
            var tenantToUse = ResolveTenantIdFromCookie() ?? model.TenantId;
            var payload = new
            {
                model.Username,
                Password = password,
                Email = model.Email,
                model.FirstName,
                model.LastName,
                model.OtherNames,
                model.PhoneNumber,
                model.Department,
                model.JobTitle,
                TenantId = tenantToUse,
                RoleIds = model.AssignedRoleIds
            };

            var response = await _api.PostRawAsync("/auth/users", payload);
            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = await response.Content.ReadAsStringAsync();
                await PopulateCreateScreenDataAsync();
                return View(model);
            }

            TempData["Success"] = "User created successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            await PopulateCreateScreenDataAsync();
            return View(model);
        }
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        try
        {
            var raw = await _api.GetAsync<System.Text.Json.JsonElement>($"/auth/users/{id}");
            if (raw.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            var user = MapEditViewModel(raw);
            user.Roles = await _api.GetAsync<RoleViewModel[]>("/auth/users/available-roles") ?? Array.Empty<RoleViewModel>();
            user.AssignedRoleIds = await _api.GetAsync<Guid[]>($"/auth/users/{id}/roles") ?? Array.Empty<Guid>();
            return View(user);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, UserEditViewModel model, string? newPassword, Microsoft.AspNetCore.Http.IFormFile? photo)
    {
        try
        {
            var updatePayload = new
            {
                model.Email,
                model.FirstName,
                model.LastName,
                model.OtherNames,
                model.PhoneNumber,
                model.Department,
                model.JobTitle,
                RoleIds = model.AssignedRoleIds ?? Array.Empty<Guid>()
            };

            var updateResponse = await _api.PutRawAsync($"/auth/users/{id}", updatePayload);
            if (!updateResponse.IsSuccessStatusCode)
            {
                TempData["Error"] = await updateResponse.Content.ReadAsStringAsync();
                model.Roles = await _api.GetAsync<RoleViewModel[]>("/auth/users/available-roles") ?? Array.Empty<RoleViewModel>();
                return View(model);
            }

            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                await _api.PostAsync<object>($"/auth/users/{id}/reset-password", new { NewPasswordPlain = newPassword });
            }

            if (photo != null && photo.Length > 0)
            {
                var photoResponse = await _api.PostFileAsync($"/api/profile/{id}/photo", photo);
                if (!photoResponse.IsSuccessStatusCode)
                {
                    TempData["Error"] = "User was updated, but profile photo upload failed.";
                    return RedirectToAction(nameof(Edit), new { id });
                }
            }

            TempData["Success"] = "User updated successfully.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            model.Roles = await _api.GetAsync<RoleViewModel[]>("/auth/users/available-roles") ?? Array.Empty<RoleViewModel>();
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminAction(Guid id, string action, string? newPassword = null)
    {
        try
        {
            switch (action?.Trim().ToLowerInvariant())
            {
                case "lock":
                    await _api.PostAsync<object>($"/auth/users/{id}/lock", new { });
                    TempData["Success"] = "User locked.";
                    break;
                case "unlock":
                    await _api.PostAsync<object>($"/auth/users/{id}/unlock", new { });
                    TempData["Success"] = "User unlocked.";
                    break;
                case "reset":
                    if (string.IsNullOrWhiteSpace(newPassword))
                    {
                        TempData["Error"] = "Temporary password is required.";
                        break;
                    }

                    await _api.PostAsync<object>($"/auth/users/{id}/reset-password", new { NewPasswordPlain = newPassword });
                    TempData["Success"] = "Password reset successfully.";
                    break;
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _api.DeleteRawAsync($"/auth/users/{id}");
            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = await response.Content.ReadAsStringAsync();
            }
            else
            {
                TempData["Success"] = "User deleted successfully.";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateCreateScreenDataAsync()
    {
        var tenantCookie = ResolveTenantIdFromCookie();
        ViewBag.CurrentTenantId = tenantCookie;
        ViewBag.Roles = await _api.GetAsync<RoleViewModel[]>("/auth/users/available-roles") ?? Array.Empty<RoleViewModel>();
        ViewBag.Tenants = tenantCookie.HasValue
            ? Array.Empty<TenantItem>()
            : (await _api.GetAsync<TenantItem[]>("/tenants") ?? Array.Empty<TenantItem>());
    }

    private Guid? ResolveTenantIdFromCookie()
    {
        var cookie = Request.Cookies["HmsTenantId"];
        return Guid.TryParse(cookie, out var tenantId) ? tenantId : null;
    }

    private static UserEditViewModel MapEditViewModel(System.Text.Json.JsonElement user)
    {
        static string? GetString(System.Text.Json.JsonElement root, string name)
        {
            foreach (var prop in root.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return prop.Value.GetString();
                }
            }

            return null;
        }

        static bool GetBool(System.Text.Json.JsonElement root, string name)
        {
            foreach (var prop in root.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase) &&
                    (prop.Value.ValueKind == System.Text.Json.JsonValueKind.True || prop.Value.ValueKind == System.Text.Json.JsonValueKind.False))
                {
                    return prop.Value.GetBoolean();
                }
            }

            return false;
        }

        static Guid? GetGuid(System.Text.Json.JsonElement root, string name)
        {
            var raw = GetString(root, name);
            return Guid.TryParse(raw, out var guid) ? guid : null;
        }

        return new UserEditViewModel
        {
            Id = GetGuid(user, "id") ?? Guid.Empty,
            Username = GetString(user, "username") ?? string.Empty,
            Email = GetString(user, "email"),
            FirstName = GetString(user, "firstName"),
            LastName = GetString(user, "lastName"),
            OtherNames = GetString(user, "otherNames"),
            PhoneNumber = GetString(user, "phoneNumber"),
            Department = GetString(user, "department"),
            JobTitle = GetString(user, "jobTitle"),
            PhotoUrl = GetString(user, "photoUrl"),
            TenantId = GetGuid(user, "tenantId"),
            TenantName = GetString(user, "tenantName"),
            IsLocked = GetBool(user, "isLocked")
        };
    }
}
