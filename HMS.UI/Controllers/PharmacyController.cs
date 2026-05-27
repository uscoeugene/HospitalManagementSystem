using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.UI.Models.Pharmacy;
using HMS.UI.Services;
using HMS.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace HMS.UI.Controllers
{
    [HMS.UI.Security.HasPermission("pharmacy.view")]
    public class PharmacyController : Controller
    {
        private readonly ApiClient _api;

        public PharmacyController(ApiClient api)
        {
            _api = api;
        }

        public async Task<IActionResult> Index()
        {
            return View();
        }

        public async Task<IActionResult> Prescriptions(Guid? patientId = null, Guid? visitId = null, string? status = null, int page = 1, int pageSize = 20)
        {
            try
            {
                var q = System.Web.HttpUtility.ParseQueryString(string.Empty);
                if (patientId.HasValue) q["patientId"] = patientId.Value.ToString();
                if (visitId.HasValue) q["visitId"] = visitId.Value.ToString();
                if (!string.IsNullOrWhiteSpace(status)) q["status"] = status;
                q["page"] = page.ToString();
                q["pageSize"] = pageSize.ToString();

                var url = "/pharmacy/prescriptions?" + q;
                var res = await _api.GetAsync<HMS.UI.Models.PagedResult<PrescriptionViewModel>>(url);
                if (res == null)
                {
                    var debug = _api.GetLastDebug();
                    if (debug?.ResponseStatus >= 500)
                    {
                        TempData["Error"] = $"API Error: {(System.Net.HttpStatusCode)debug.ResponseStatus.Value} - {ExtractApiError(debug.ResponseBody)}";
                    }
                }

                return View(res ?? new HMS.UI.Models.PagedResult<PrescriptionViewModel> { Items = Array.Empty<PrescriptionViewModel>(), Page = page, PageSize = pageSize });
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(new HMS.UI.Models.PagedResult<PrescriptionViewModel>());
            }
        }

        [HMS.UI.Security.HasPermission("pharmacy.create")]
        public async Task<IActionResult> CreatePrescription(Guid? patientId = null, Guid? visitId = null, string? returnUrl = null)
        {
            var vm = await BuildCreatePrescriptionViewModelAsync(patientId, visitId, returnUrl);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("pharmacy.create")]
        public async Task<IActionResult> CreatePrescription(CreatePrescriptionViewModel model)
        {
            try
            {
                var items = (model.Items ?? Array.Empty<CreatePrescriptionItemViewModel>())
                    .Where(i => i.Quantity > 0 && (!string.IsNullOrWhiteSpace(i.MedicationName) || i.InventoryItemId.HasValue))
                    .Select(i => new
                    {
                        i.InventoryItemId,
                        MedicationName = i.MedicationName,
                        i.Dosage,
                        i.Frequency,
                        i.Quantity
                    })
                    .ToArray();

                var payload = new
                {
                    model.PatientId,
                    model.VisitId,
                    Items = items
                };

                var response = await _api.PostRawAsync("/pharmacy/prescriptions", payload);
                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = await response.Content.ReadAsStringAsync();
                    model = await RehydrateCreatePrescriptionViewModelAsync(model);
                    return View(model);
                }

                TempData["Success"] = "Prescription created and sent to pharmacy.";
                if (!string.IsNullOrWhiteSpace(model.ReturnUrl))
                {
                    return LocalRedirect(model.ReturnUrl);
                }

                return RedirectToAction(nameof(Prescriptions), new { patientId = model.PatientId, visitId = model.VisitId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                model = await RehydrateCreatePrescriptionViewModelAsync(model);
                return View(model);
            }
        }

        public async Task<IActionResult> PrescriptionDetails(Guid id)
        {
            try
            {
                var prescription = await _api.GetAsync<PrescriptionViewModel>($"/pharmacy/prescriptions/{id}");
                if (prescription == null) return NotFound();

                ViewBag.AvailableInventoryItems = await _api.GetAsync<InventoryItemViewModel[]>("/pharmacy/inventory") ?? Array.Empty<InventoryItemViewModel>();
                return View(prescription);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Prescriptions));
            }
        }

        [HMS.UI.Security.HasPermission("pharmacy.create")]
        public async Task<IActionResult> EditPrescription(Guid id)
        {
            try
            {
                var prescription = await _api.GetAsync<PrescriptionViewModel>($"/pharmacy/prescriptions/{id}");
                if (prescription == null) return NotFound();

                var vm = new EditPrescriptionViewModel
                {
                    Prescription = prescription,
                    AvailableInventoryItems = await _api.GetAsync<InventoryItemViewModel[]>("/pharmacy/inventory") ?? Array.Empty<InventoryItemViewModel>()
                };

                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Prescriptions));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("pharmacy.create")]
        public async Task<IActionResult> UpdatePrescription(Guid id, Guid patientId, Guid? visitId)
        {
            try
            {
                var response = await _api.PutRawAsync($"/pharmacy/prescriptions/{id}", new { PatientId = patientId, VisitId = visitId });
                TempData[response.IsSuccessStatusCode ? "Success" : "Error"] = response.IsSuccessStatusCode
                    ? "Prescription updated."
                    : await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(EditPrescription), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("pharmacy.create")]
        public async Task<IActionResult> UpdatePrescriptionItems(Guid id, CreatePrescriptionViewModel form, bool allowIfDispensed = false)
        {
            try
            {
                var items = (form.Items ?? Array.Empty<CreatePrescriptionItemViewModel>())
                    .Where(i => i.Quantity > 0 && (!string.IsNullOrWhiteSpace(i.MedicationName) || i.InventoryItemId.HasValue))
                    .Select(i => new
                    {
                        i.InventoryItemId,
                        MedicationName = i.MedicationName,
                        i.Dosage,
                        i.Frequency,
                        i.Quantity
                    })
                    .ToArray();

                var response = await _api.PutRawAsync($"/pharmacy/prescriptions/{id}/items", new { Items = items, AllowIfDispensed = allowIfDispensed });
                TempData[response.IsSuccessStatusCode ? "Success" : "Error"] = response.IsSuccessStatusCode
                    ? "Prescription items updated."
                    : await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(EditPrescription), new { id });
        }

        [HttpGet]
        [HMS.UI.Security.HasPermission("pharmacy.create")]
        public async Task<IActionResult> PatientVisits(Guid patientId)
        {
            var visits = await LoadVisitOptionsAsync(patientId);
            return Json(visits);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("pharmacy.dispense")]
        public async Task<IActionResult> ReconcilePrescriptionItem(Guid prescriptionId, Guid itemId, Guid? inventoryItemId, string? substituteMedicationName, string? status, string? note)
        {
            try
            {
                var response = await _api.PutRawAsync($"/pharmacy/prescriptions/{prescriptionId}/items/{itemId}/reconcile", new
                {
                    InventoryItemId = inventoryItemId,
                    SubstituteMedicationName = substituteMedicationName,
                    Status = status,
                    Note = note
                });

                TempData[response.IsSuccessStatusCode ? "Success" : "Error"] = response.IsSuccessStatusCode
                    ? "Prescription item updated for pharmacy review."
                    : await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(PrescriptionDetails), new { id = prescriptionId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("pharmacy.dispense")]
        public async Task<IActionResult> Dispense(Guid prescriptionId, Guid itemId, int quantity, Guid? inventoryItemId = null, string? dispensedMedicationName = null, bool allowOnCredit = false, string? reason = null, string? note = null)
        {
            try
            {
                var response = await _api.PostRawAsync("/pharmacy/dispense", new
                {
                    PrescriptionItemId = itemId,
                    InventoryItemId = inventoryItemId,
                    DispensedMedicationName = dispensedMedicationName,
                    Quantity = quantity,
                    AllowOnCredit = allowOnCredit,
                    CreditReason = reason,
                    Note = note
                });

                TempData[response.IsSuccessStatusCode ? "Success" : "Error"] = response.IsSuccessStatusCode
                    ? "Medication dispensed and billing created."
                    : await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(PrescriptionDetails), new { id = prescriptionId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("pharmacy.dispense")]
        public async Task<IActionResult> AddPrescriptionNote(Guid prescriptionId, Guid itemId, string note)
        {
            try
            {
                var response = await _api.PostRawAsync($"/pharmacy/prescriptions/{prescriptionId}/items/{itemId}/notes", note);
                TempData[response.IsSuccessStatusCode ? "Success" : "Error"] = response.IsSuccessStatusCode
                    ? "Note added."
                    : await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(PrescriptionDetails), new { id = prescriptionId });
        }

        public async Task<IActionResult> Reports()
        {
            try
            {
                var shortages = await _api.GetAsync<StockShortageViewModel[]>("/api/reports/pharmacy/shortages");
                var daily = await _api.GetAsync<DailyDispenseViewModel[]>("/api/reports/pharmacy/daily-dispenses");
                var revenue = await _api.GetAsync<InventoryRevenueViewModel[]>("/api/reports/pharmacy/revenue-per-inventory");

                var vm = new PharmacyReportsViewModel
                {
                    Shortages = shortages ?? Array.Empty<StockShortageViewModel>(),
                    Daily = daily ?? Array.Empty<DailyDispenseViewModel>(),
                    Revenue = revenue ?? Array.Empty<InventoryRevenueViewModel>()
                };
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(new PharmacyReportsViewModel());
            }
        }

        [HMS.UI.Security.HasPermission("pharmacy.inventory.manage")]
        public IActionResult CreateInventory()
        {
            return View(new CreateInventoryItemViewModel());
        }

        public async Task<IActionResult> Inventory(string? category = null)
        {
            try
            {
                var items = await _api.GetAsync<InventoryItemViewModel[]>("/pharmacy/inventory" + (string.IsNullOrWhiteSpace(category) ? string.Empty : $"?category={Uri.EscapeDataString(category)}"));
                return View(items ?? Array.Empty<InventoryItemViewModel>());
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(Array.Empty<InventoryItemViewModel>());
            }
        }

        public async Task<IActionResult> InventoryDetails(Guid id)
        {
            try
            {
                var item = await _api.GetAsync<InventoryItemViewModel>($"/pharmacy/inventory/{id}");
                if (item == null) return NotFound();
                return View(item);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Inventory));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("pharmacy.inventory.manage")]
        public async Task<IActionResult> CreateInventory(CreateInventoryItemViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            try
            {
                var payload = new
                {
                    model.Code,
                    model.Name,
                    model.Description,
                    model.UnitPrice,
                    model.Currency,
                    model.Stock,
                    model.Category,
                    model.Unit
                };

                var response = await _api.PostRawAsync("/pharmacy/inventory", payload);
                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = await response.Content.ReadAsStringAsync();
                    return View(model);
                }

                TempData["Success"] = "Inventory item created";
                return RedirectToAction(nameof(Inventory));
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("pharmacy.inventory.manage")]
        public async Task<IActionResult> UpdateInventory(Guid id, UpdateInventoryItemViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            try
            {
                var response = await _api.PutRawAsync($"/pharmacy/inventory/{id}", new
                {
                    model.Code,
                    model.Name,
                    model.Description,
                    model.UnitPrice,
                    model.Currency,
                    model.Category,
                    model.Unit
                });

                TempData[response.IsSuccessStatusCode ? "Success" : "Error"] = response.IsSuccessStatusCode
                    ? "Inventory item updated"
                    : await response.Content.ReadAsStringAsync();
                return RedirectToAction(nameof(InventoryDetails), new { id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("pharmacy.delete")]
        public async Task<IActionResult> DeleteInventory(Guid id)
        {
            try
            {
                var response = await _api.DeleteRawAsync($"/pharmacy/inventory/{id}");
                TempData[response.IsSuccessStatusCode ? "Success" : "Error"] = response.IsSuccessStatusCode
                    ? "Deleted"
                    : await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Inventory));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("pharmacy.inventory.manage")]
        public async Task<IActionResult> AdjustStock(Guid id, int delta)
        {
            try
            {
                var response = await _api.PostRawAsync($"/pharmacy/inventory/{id}/adjust-stock", new { Delta = delta });
                TempData[response.IsSuccessStatusCode ? "Success" : "Error"] = response.IsSuccessStatusCode
                    ? "Stock adjusted"
                    : await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(InventoryDetails), new { id });
        }

        private async Task<CreatePrescriptionViewModel> BuildCreatePrescriptionViewModelAsync(Guid? patientId, Guid? visitId, string? returnUrl)
        {
            var patientsPage = await _api.GetAsync<PagedResult<PatientListItemViewModel>>("/patients?page=1&pageSize=200");
            var patientOptions = (patientsPage?.Items ?? Array.Empty<PatientListItemViewModel>())
                .Select(p => new PrescriptionPatientOptionViewModel
                {
                    Id = p.Id,
                    DisplayName = string.Join(" ", new[] { p.FirstName, p.MiddleName, p.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim()
                })
                .OrderBy(p => p.DisplayName)
                .ToArray();

            var normalizedPatientId = patientId ?? patientOptions.FirstOrDefault()?.Id ?? Guid.Empty;
            var visitOptions = normalizedPatientId != Guid.Empty
                ? await LoadVisitOptionsAsync(normalizedPatientId)
                : Array.Empty<PrescriptionVisitOptionViewModel>();

            var normalizedVisitId = visitId.HasValue && visitOptions.Any(v => v.Id == visitId.Value)
                ? visitId
                : visitOptions.FirstOrDefault()?.Id;

            return new CreatePrescriptionViewModel
            {
                PatientId = normalizedPatientId,
                VisitId = normalizedVisitId,
                ReturnUrl = returnUrl,
                LockPatientSelection = patientId.HasValue,
                LockVisitSelection = visitId.HasValue,
                Items = new[] { new CreatePrescriptionItemViewModel { Quantity = 1 } },
                AvailableInventoryItems = await _api.GetAsync<InventoryItemViewModel[]>("/pharmacy/inventory") ?? Array.Empty<InventoryItemViewModel>(),
                AvailablePatients = patientOptions,
                AvailableVisits = visitOptions
            };
        }

        private async Task<CreatePrescriptionViewModel> RehydrateCreatePrescriptionViewModelAsync(CreatePrescriptionViewModel model)
        {
            var baseVm = await BuildCreatePrescriptionViewModelAsync(
                model.LockPatientSelection ? model.PatientId : null,
                model.LockVisitSelection ? model.VisitId : null,
                model.ReturnUrl);

            baseVm.PatientId = model.PatientId;
            baseVm.VisitId = model.VisitId;
            baseVm.Items = (model.Items ?? Array.Empty<CreatePrescriptionItemViewModel>()).Length == 0
                ? new[] { new CreatePrescriptionItemViewModel { Quantity = 1 } }
                : model.Items;
            baseVm.LockPatientSelection = model.LockPatientSelection;
            baseVm.LockVisitSelection = model.LockVisitSelection;
            return baseVm;
        }

        private async Task<PrescriptionVisitOptionViewModel[]> LoadVisitOptionsAsync(Guid patientId)
        {
            var visits = await _api.GetAsync<VisitViewModel[]>($"/patients/{patientId}/visits") ?? Array.Empty<VisitViewModel>();
            return visits
                .OrderByDescending(v => v.VisitAt)
                .Select(v => new PrescriptionVisitOptionViewModel
                {
                    Id = v.Id,
                    DisplayName = $"{v.VisitType} - {v.VisitAt:yyyy-MM-dd HH:mm}"
                })
                .ToArray();
        }

        private static string ExtractApiError(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(raw);
                var root = document.RootElement;

                if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (root.TryGetProperty("error", out var error))
                    {
                        if (error.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            return error.GetString() ?? string.Empty;
                        }

                        if (error.ValueKind == System.Text.Json.JsonValueKind.Object &&
                            error.TryGetProperty("message", out var message) &&
                            message.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            return message.GetString() ?? string.Empty;
                        }
                    }

                    if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        return detail.GetString() ?? string.Empty;
                    }
                }
            }
            catch
            {
            }

            return raw;
        }
    }
}
