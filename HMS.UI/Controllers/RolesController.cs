using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.UI.Models.Roles;
using HMS.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace HMS.UI.Controllers
{
    [HMS.UI.Security.HasPermission("roles.manage")]
    public class RolesController : Controller
    {
        private readonly ApiClient _api;

        public RolesController(ApiClient api)
        {
            _api = api;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var roles = await _api.GetAsync<RoleListItemViewModel[]>("/roles");
                return View(roles ?? Array.Empty<RoleListItemViewModel>());
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(Array.Empty<RoleListItemViewModel>());
            }
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] string name, [FromForm] string description)
        {
            try
            {
                var payload = new { Name = name, Description = description };
                await _api.PostAsync<object>("/roles", payload);
                TempData["Success"] = "Role created";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View();
            }
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            try
            {
                var vm = await LoadEditViewModelAsync(id);
                if (vm == null) return NotFound();
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, RoleEditViewModel vm)
        {
            try
            {
                var payload = new { Name = vm.Name, Description = vm.Description };
                await _api.PutAsync<object>($"/roles/{id}", payload);
                TempData["Success"] = "Role updated";
                return RedirectToAction(nameof(Edit), new { id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                var fullVm = await LoadEditViewModelAsync(id) ?? vm;
                fullVm.Name = vm.Name;
                fullVm.Description = vm.Description;
                return View(fullVm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPermission(Guid id, [FromForm] string code)
        {
            try
            {
                var payload = new { Code = code, Description = string.Empty };
                await _api.PostAsync<object>($"/roles/{id}/permissions", payload);
                TempData["Success"] = "Permission added";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Edit), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePermission(Guid id, [FromForm] string code, [FromForm] string description)
        {
            if (!CanManagePermissionCatalog())
            {
                TempData["Error"] = "Only system administrators can register new permissions.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            try
            {
                await _api.PostAsync<object>("/roles/permissions", new { Code = code, Description = description });
                TempData["Success"] = "Permission registered";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Edit), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemovePermission(Guid id, [FromForm] string code)
        {
            try
            {
                await _api.DeleteRawAsync($"/roles/{id}/permissions/{Uri.EscapeDataString(code)}");
                TempData["Success"] = "Permission removed";
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
                await _api.DeleteRawAsync($"/roles/{id}");
                TempData["Success"] = "Role deleted";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        private bool CanManagePermissionCatalog()
        {
            return User.HasClaim("permission", "system.permissions.manage");
        }

        private async Task<RoleEditViewModel?> LoadEditViewModelAsync(Guid id)
        {
            var role = await _api.GetAsync<RoleEditViewModel>($"/roles/{id}");
            if (role == null)
            {
                return null;
            }

            var permissions = await _api.GetAsync<PermissionOptionViewModel[]>("/roles/permissions") ?? Array.Empty<PermissionOptionViewModel>();
            role.AvailablePermissions = permissions
                .OrderBy(p => p.Code)
                .ToList();
            role.CanManagePermissionCatalog = CanManagePermissionCatalog();
            return role;
        }
    }
}
