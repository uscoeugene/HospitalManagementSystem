using System;
using System.Threading.Tasks;
using HMS.UI.Models.Profile;
using HMS.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace HMS.UI.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ApiClient _api;

        public ProfileController(ApiClient api)
        {
            _api = api;
        }

        [HttpGet]
        [HMS.UI.Security.HasPermission("PROFILE.READ")]
        public async Task<IActionResult> Me()
        {
            var vm = await _api.GetAsync<UserProfileViewModel>("/api/Profile/me");
            if (vm == null) vm = new UserProfileViewModel();
            return View(vm);
        }

        [HttpGet]
        [HMS.UI.Security.HasPermission("PROFILE.UPDATE")]
        public IActionResult Security()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("PROFILE.UPDATE")]
        public async Task<IActionResult> ChangePassword([FromForm] string currentPassword, [FromForm] string newPassword, [FromForm] string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            {
                TempData["Error"] = "Current and new password are required";
                return RedirectToAction("Security");
            }

            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "New passwords do not match";
                return RedirectToAction("Security");
            }

            try
            {
                await _api.PostAsync<object>("/auth/change-password", new { CurrentPassword = currentPassword, NewPassword = newPassword });
                TempData["Success"] = "Password changed";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Security");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("PROFILE.UPDATE")]
        public async Task<IActionResult> UploadAvatar()
        {
            var file = Request.Form.Files.FirstOrDefault();
            if (file == null)
            {
                TempData["Error"] = "File required";
                return RedirectToAction("Me");
            }

            try
            {
                var resp = await _api.PostFileAsync("/api/profile/me/photo", file);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    TempData["Error"] = "Upload failed: " + body;
                    return RedirectToAction("Me");
                }

                var json = await resp.Content.ReadAsStringAsync();
                try
                {
                    using var jd = System.Text.Json.JsonDocument.Parse(json);
                    if (jd.RootElement.TryGetProperty("url", out var u))
                    {
                        TempData["Success"] = "Avatar uploaded";
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Me");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("PROFILE.UPDATE")]
        public async Task<IActionResult> Me(UserProfileViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var isCreate = model.Id == Guid.Empty;
            object payload;
            if (isCreate)
            {
                payload = new
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    OtherNames = model.OtherNames,
                    Gender = model.Gender,
                    DateOfBirth = model.DateOfBirth,
                    PhoneNumber = model.PhoneNumber,
                    Email = model.Email,
                    Address = model.Address,
                    PhotoUrl = model.PhotoUrl,
                    StaffNumber = model.StaffNumber,
                    Department = model.Department,
                    JobTitle = model.JobTitle,
                    IsMedicalStaff = model.IsMedicalStaff
                };
            }
            else
            {
                payload = new
                {
                    FirstName = string.IsNullOrWhiteSpace(model.FirstName) ? null : model.FirstName,
                    LastName = string.IsNullOrWhiteSpace(model.LastName) ? null : model.LastName,
                    OtherNames = string.IsNullOrWhiteSpace(model.OtherNames) ? null : model.OtherNames,
                    Gender = string.IsNullOrWhiteSpace(model.Gender) ? null : model.Gender,
                    DateOfBirth = model.DateOfBirth,
                    PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber,
                    Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email,
                    Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address,
                    PhotoUrl = string.IsNullOrWhiteSpace(model.PhotoUrl) ? null : model.PhotoUrl,
                    StaffNumber = string.IsNullOrWhiteSpace(model.StaffNumber) ? null : model.StaffNumber,
                    Department = string.IsNullOrWhiteSpace(model.Department) ? null : model.Department,
                    JobTitle = string.IsNullOrWhiteSpace(model.JobTitle) ? null : model.JobTitle,
                    IsMedicalStaff = model.IsMedicalStaff
                };
            }

            var raw = await _api.PutRawAsync(isCreate ? "/api/Profile/me" : "/api/Profile/me", payload);
            if (!raw.IsSuccessStatusCode)
            {
                var body = await raw.Content.ReadAsStringAsync();
                TempData["Error"] = "Failed: " + body;
                return View(model);
            }

            TempData["Success"] = "Profile updated successfully";
            return RedirectToAction(nameof(Me));
        }

        [HttpGet]
        [HMS.UI.Security.HasPermission("PROFILE.READ")]
        public async Task<IActionResult> User(Guid id)
        {
            var vm = await _api.GetAsync<UserProfileViewModel>($"/api/Profile/{id}");
            if (vm == null) vm = new UserProfileViewModel { UserId = id };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("PROFILE.MANAGE")]
        public async Task<IActionResult> User(Guid id, UserProfileViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var payload = new
            {
                FirstName = string.IsNullOrWhiteSpace(model.FirstName) ? null : model.FirstName,
                LastName = string.IsNullOrWhiteSpace(model.LastName) ? null : model.LastName,
                OtherNames = string.IsNullOrWhiteSpace(model.OtherNames) ? null : model.OtherNames,
                Gender = string.IsNullOrWhiteSpace(model.Gender) ? null : model.Gender,
                DateOfBirth = model.DateOfBirth,
                PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber,
                Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email,
                Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address,
                PhotoUrl = string.IsNullOrWhiteSpace(model.PhotoUrl) ? null : model.PhotoUrl,
                StaffNumber = string.IsNullOrWhiteSpace(model.StaffNumber) ? null : model.StaffNumber,
                Department = string.IsNullOrWhiteSpace(model.Department) ? null : model.Department,
                JobTitle = string.IsNullOrWhiteSpace(model.JobTitle) ? null : model.JobTitle,
                IsMedicalStaff = model.IsMedicalStaff
            };

            var raw = await _api.PutRawAsync($"/api/Profile/{id}", payload);
            if (!raw.IsSuccessStatusCode)
            {
                var body = await raw.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, "Failed to update profile");
                TempData["Error"] = body;
                return View(model);
            }

            TempData["Success"] = "Profile updated";
            return RedirectToAction("User", new { id = id });
        }

        [HttpGet]
        public async Task<IActionResult> Summary(int page = 1, int pageSize = 50)
        {
            var res = await _api.GetAsync<HMS.UI.Models.Reporting.PagedReportResult<HMS.UI.Models.Reporting.ProfileSummaryDto>>($"/reports/Profile/summary?page={page}&pageSize={pageSize}");
            return View(res ?? new HMS.UI.Models.Reporting.PagedReportResult<HMS.UI.Models.Reporting.ProfileSummaryDto>());
        }
    }
}
