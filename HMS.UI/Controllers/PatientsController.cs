using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using HMS.UI.Services;
using System.Text.Json;
using HMS.UI.Models;
using System.Net.Http.Json;
using HMS.UI.Security;

namespace HMS.UI.Controllers
{
    [HasPermission("patients.view")]
    public class PatientsController : Controller
    {
        private readonly ApiClient _api;
        private readonly StaticDataService _static;

        public PatientsController(ApiClient api, StaticDataService staticData)
        {
            _api = api;
            _static = staticData;
        }

        private async Task PopulateStaticListsAsync()
        {
            var med = await _static.GetMedicalDataAsync();
            ViewBag.BloodGroups = med?.BloodGroups ?? Enumerable.Empty<string>();
            ViewBag.Genotypes = med?.Genotypes ?? Enumerable.Empty<string>();

            var countries = await _static.GetCountriesAsync() ?? Array.Empty<StaticDataService.CountryEntry>();
            ViewData["Countries"] = countries.Select(c => c.Name).ToArray();
            ViewData["CountryStatesJson"] = JsonSerializer.Serialize(countries);
        }
        [Route("patients")]
        public async Task<IActionResult> Index(string q = null, int page = 1)
        {
            try
            {
                var res = await _api.GetAsync<PagedResult<PatientListItemViewModel>>($"/patients?q={System.Net.WebUtility.UrlEncode(q ?? string.Empty)}&page={page}");

                if (res == null)
                {
                    // Inspect API debug info to provide a helpful message instead of silently showing empty list
                    try
                    {
                        var dbg = _api.GetLastDebug();
                        if (dbg != null && dbg.ResponseStatus.HasValue)
                        {
                            var status = dbg.ResponseStatus.Value;
                            var body = dbg.ResponseBody ?? string.Empty;
                            if (status == 401)
                            {
                                // Not authenticated - redirect to login
                                return RedirectToAction("Login", "Account");
                            }
                            if (status == 403)
                            {
                                TempData["Error"] = "Access denied when fetching patients.";
                                return View(new PagedResult<PatientListItemViewModel>());
                            }

                            // Try parse common shapes: { error: ".." } or ProblemDetails
                            string friendly = null;
                            try
                            {
                                using var jd = System.Text.Json.JsonDocument.Parse(body);
                                var root = jd.RootElement;
                                if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                                {
                                    if (root.TryGetProperty("error", out var e) && e.ValueKind == System.Text.Json.JsonValueKind.String) friendly = e.GetString();
                                    else if (root.TryGetProperty("detail", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.String) friendly = d.GetString();
                                    else if (root.TryGetProperty("title", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String) friendly = t.GetString();
                                }
                            }
                            catch { }

                            TempData["Error"] = string.IsNullOrWhiteSpace(friendly) ? ($"Failed to load patients (API {status})") : friendly;
                        }
                    }
                    catch { }

                    return View(new PagedResult<PatientListItemViewModel>());
                }

                return View(res);
            }
            catch
            {
                return View(new PagedResult<PatientListItemViewModel>());
            }
        }

        [HasPermission("patients.manage")]
        public async Task<IActionResult> Create()
        {
            await PopulateStaticListsAsync();
            return View(new PatientCreateViewModel { DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> Create(PatientCreateViewModel vm)
        {
            // If model binding failed or validation invalid, attempt to read raw form values as a fallback.
            if (!ModelState.IsValid || vm == null)
            {
                try
                {
                    var f = Request.HasFormContentType ? Request.Form : null;
                    if (f != null)
                    {
                        // build payload from form values (use ISO date string)
                        var dob = f["DateOfBirth"].ToString();
                        var payload = new
                        {
                            FirstName = f["FirstName"].ToString(),
                            MiddleName = string.IsNullOrWhiteSpace(f["MiddleName"]) ? null : f["MiddleName"].ToString(),
                            LastName = f["LastName"].ToString(),
                            DateOfBirth = dob, // yyyy-MM-dd from date input
                            Gender = f["Gender"].ToString(),
                            Phone = string.IsNullOrWhiteSpace(f["Phone"]) ? null : f["Phone"].ToString(),
                            AlternatePhone = string.IsNullOrWhiteSpace(f["AlternatePhone"]) ? null : f["AlternatePhone"].ToString(),
                            Email = string.IsNullOrWhiteSpace(f["Email"]) ? null : f["Email"].ToString(),
                            MedicalRecordNumber = string.IsNullOrWhiteSpace(f["MedicalRecordNumber"]) ? null : f["MedicalRecordNumber"].ToString(),
                        };

                        var resp2 = await _api.PostAsync<object>("/patients", payload);
                        if (resp2 != null)
                        {
                            TempData["Success"] = "Patient created successfully.";
                            return RedirectToAction("Index");
                        }
                        else
                        {
                            ModelState.AddModelError(string.Empty, "Failed to create patient.");
                            TempData["Error"] = "Failed to create patient.";
                            return View(vm ?? new PatientCreateViewModel());
                        }
                    }
                }
                catch (Exception ex)
                {
                    TempData["Error"] = ex.Message;
                    // fall through to show view with errors
                }
            }

            // Normal binding path
            try
            {
                var resp = await _api.PostAsync<object>("/patients", vm!);
                if (resp != null)
                {
                    TempData["Success"] = "Patient created successfully.";
                    return RedirectToAction("Index");
                }
                ModelState.AddModelError(string.Empty, "Failed to create patient.");
                TempData["Error"] = "Failed to create patient.";
                await PopulateStaticListsAsync();
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(vm);
            }

        }


        public async Task<IActionResult> Details(Guid id)
        {
            try
            {
                var p = await _api.GetAsync<PatientDetailsViewModel>($"/patients/{id}");
                if (p == null) return NotFound();
                return View(p);
            }
            catch
            {
                return NotFound();
            }
        }

        [HasPermission("patients.manage")]
        public async Task<IActionResult> Edit(Guid id)
        {
            try
            {
                var p = await _api.GetAsync<PatientDetailsViewModel>($"/patients/{id}");
                if (p == null) return NotFound();
                var vm = new PatientCreateViewModel
                {
                    Id = p.Id,
                    FirstName = p.FirstName,
                    MiddleName = p.MiddleName,
                    LastName = p.LastName,
                    DateOfBirth = p.DateOfBirth,
                    Gender = p.Gender,
                    Phone = p.Phone,
                    AlternatePhone = p.AlternatePhone,
                    Email = p.Email,
                    MedicalRecordNumber = p.MedicalRecordNumber,
                    AddressLine1 = p.AddressLine1,
                    AddressLine2 = p.AddressLine2,
                    City = p.City,
                    State = p.State,
                    PostalCode = p.PostalCode,
                    Country = p.Country,
                    Nationality = p.Nationality,
                    NationalIdNumber = p.NationalIdNumber,
                    BloodGroup = p.BloodGroup,
                    Genotype = p.Genotype,
                    EmergencyContactName = p.EmergencyContactName,
                    EmergencyContactRelationship = p.EmergencyContactRelationship,
                    EmergencyContactPhone = p.EmergencyContactPhone,
                    InsuranceProvider = p.InsuranceProvider,
                    InsuranceNumber = p.InsuranceNumber,
                    Occupation = p.Occupation,
                    IsActive = p.IsActive
                };
                return View(vm);
            }
            catch
            {
                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> Edit(PatientCreateViewModel vm)
        {
            if (vm == null || !ModelState.IsValid)
            {
                return View(vm ?? new PatientCreateViewModel());
            }

            try
            {
                var id = vm.Id ?? Guid.Empty;
                if (id == Guid.Empty) return BadRequest();

                // build payload similar to create
                var payload = new
                {
                    FirstName = vm.FirstName.Trim(),
                    MiddleName = string.IsNullOrWhiteSpace(vm.MiddleName) ? null : vm.MiddleName,
                    LastName = vm.LastName,
                    DateOfBirth = vm.DateOfBirth,
                    Gender = vm.Gender,
                    Phone = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone,
                    AlternatePhone = string.IsNullOrWhiteSpace(vm.AlternatePhone) ? null : vm.AlternatePhone,
                    Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email,
                    MedicalRecordNumber = string.IsNullOrWhiteSpace(vm.MedicalRecordNumber) ? null : vm.MedicalRecordNumber,
                    AddressLine1 = vm.AddressLine1,
                    AddressLine2 = vm.AddressLine2,
                    City = vm.City,
                    State = vm.State,
                    PostalCode = vm.PostalCode,
                    Country = vm.Country,
                    Nationality = vm.Nationality,
                    NationalIdNumber = vm.NationalIdNumber,
                    BloodGroup = vm.BloodGroup,
                    Genotype = vm.Genotype,
                    EmergencyContactName = vm.EmergencyContactName,
                    EmergencyContactRelationship = vm.EmergencyContactRelationship,
                    EmergencyContactPhone = vm.EmergencyContactPhone,
                    InsuranceProvider = vm.InsuranceProvider,
                    InsuranceNumber = vm.InsuranceNumber,
                    Occupation = vm.Occupation
                    ,
                    IsActive = vm.IsActive
                };

                var resp = await _api.PutAsync<object>($"/patients/{id}", payload);
                if (resp != null)
                {
                    TempData["Success"] = "Patient updated successfully.";
                    return RedirectToAction("Details", new { id = id });
                }

                TempData["Error"] = "Failed to update patient.";
                ModelState.AddModelError(string.Empty, "Failed to update patient.");
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(vm);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Search(string? q, int page = 1)
        {
            try
            {
                var res = await _api.GetAsync<PagedResult<PatientListItemViewModel>>(
                    $"/patients?q={System.Net.WebUtility.UrlEncode(q ?? string.Empty)}&page={page}");

                if (res == null)
                {
                    return PartialView(
                        "_PatientsTable",
                        new PagedResult<PatientListItemViewModel>());
                }

                return PartialView("_PatientsTable", res);
            }
            catch
            {
                return PartialView(
                    "_PatientsTable",
                    new PagedResult<PatientListItemViewModel>());
            }
        }
    }
}
