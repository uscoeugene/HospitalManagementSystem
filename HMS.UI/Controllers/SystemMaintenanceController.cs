using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HMS.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace HMS.UI.Controllers
{
    [HMS.UI.Security.HasPermission("system.maintenance.manage")]
    public class SystemMaintenanceController : Controller
    {
        private readonly ApiClient _api;

        public SystemMaintenanceController(ApiClient api)
        {
            _api = api;
        }

        public async Task<IActionResult> Index()
        {
            var vm = await BuildViewModelAsync();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Run(HMS.UI.Models.SystemMaintenanceViewModel model)
        {
            try
            {
                var payload = new
                {
                    Scope = model.SelectedScope,
                    TenantId = model.SelectedTenantId,
                    TenantCodeConfirmation = model.TenantCodeConfirmation,
                    Confirmation = model.Confirmation
                };

                await _api.PostAsync<object>("/system-maintenance/run", payload);
                TempData["Success"] = "Maintenance action completed.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                var vm = await BuildViewModelAsync(model.SelectedScope, model.SelectedTenantId, model.Confirmation, model.TenantCodeConfirmation);
                return View(nameof(Index), vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReseedAuth()
        {
            try
            {
                await _api.PostAsync<object>("/system-maintenance/reseed-auth", new { });
                TempData["Success"] = "Auth seed refreshed.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReseedPlatform()
        {
            try
            {
                await _api.PostAsync<object>("/system-maintenance/reseed-platform", new { });
                TempData["Success"] = "Platform reseed completed.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetTenantAuth(Guid tenantId)
        {
            try
            {
                await _api.PostAsync<object>($"/system-maintenance/tenants/{tenantId}/reset-auth", new { });
                TempData["Success"] = "Tenant auth data reset.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<HMS.UI.Models.SystemMaintenanceViewModel> BuildViewModelAsync(string? selectedScope = null, Guid? selectedTenantId = null, string? confirmation = null, string? tenantCodeConfirmation = null)
        {
            var tenants = await LoadTenantsAsync();
            return new HMS.UI.Models.SystemMaintenanceViewModel
            {
                Tenants = tenants.ToList(),
                Scopes = new List<HMS.UI.Models.MaintenanceScopeOptionViewModel>
                {
                    new() { Value = "auth-seed", Label = "Auth catalog refresh", Description = "Refreshes built-in roles, permissions, and seeded auth users." , RequiresTenant = false, RequiresTenantCodeConfirmation = false },
                    new() { Value = "platform-seed", Label = "Platform reseed", Description = "Refreshes auth seed and platform seed data together.", RequiresTenant = false, RequiresTenantCodeConfirmation = false },
                    new() { Value = "tenant-auth-reset", Label = "Tenant auth reset", Description = "Deletes tenant-owned users, roles, memberships, tokens, and tenant auth metadata before reseeding the auth catalog.", RequiresTenant = true, RequiresTenantCodeConfirmation = true }
                },
                SelectedScope = string.IsNullOrWhiteSpace(selectedScope) ? "tenant-auth-reset" : selectedScope,
                SelectedTenantId = selectedTenantId,
                Confirmation = confirmation,
                TenantCodeConfirmation = tenantCodeConfirmation
            };
        }

        private async Task<IEnumerable<HMS.UI.Models.TenantItem>> LoadTenantsAsync()
        {
            return await _api.GetAsync<HMS.UI.Models.TenantItem[]>("/tenants") ?? Array.Empty<HMS.UI.Models.TenantItem>();
        }
    }
}
