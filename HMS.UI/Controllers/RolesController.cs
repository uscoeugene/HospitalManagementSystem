using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using HMS.UI.Services;
using HMS.UI.Models.Roles;

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
                var roles = await _api.GetAsync<HMS.UI.Models.Roles.RoleListItemViewModel[]>("/roles");
                return View(roles ?? Array.Empty<HMS.UI.Models.Roles.RoleListItemViewModel>());
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(Array.Empty<HMS.UI.Models.Roles.RoleListItemViewModel>());
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
                var created = await _api.PostAsync<object>("/roles", payload);
                TempData["Success"] = "Role created";
                return RedirectToAction("Index");
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
                var role = await _api.GetAsync<HMS.UI.Models.Roles.RoleEditViewModel>($"/roles/{id}");
                if (role == null) return NotFound();
                return View(role);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index");
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
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPermission(Guid id, [FromForm] string code, [FromForm] string description)
        {
            try
            {
                var payload = new { Code = code, Description = description };
                await _api.PostAsync<object>($"/roles/{id}/permissions", payload);
                TempData["Success"] = "Permission added";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("Edit", new { id = id });
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
            return RedirectToAction("Edit", new { id = id });
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
            return RedirectToAction("Index");
        }
    }
}
