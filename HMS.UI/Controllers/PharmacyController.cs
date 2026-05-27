using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.UI.Models.Pharmacy;
using HMS.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace HMS.UI.Controllers
{
    [HMS.UI.Security.HasPermission("pharmacy.view")]
    public class PharmacyController : Controller
    {
        private readonly ApiClient _api;
        public PharmacyController(ApiClient api) { _api = api; }

        public async Task<IActionResult> Index()
        {
            // basic dashboard - links to modules
            return View();
        }

        // Drug UI removed. Inventory pages and management are available under Inventory()

        // Prescriptions
        public async Task<IActionResult> Prescriptions(Guid? patientId = null, string? status = null, int page = 1, int pageSize = 20)
        {
            try
            {
                var q = System.Web.HttpUtility.ParseQueryString(string.Empty);
                if (patientId.HasValue) q["patientId"] = patientId.Value.ToString();
                if (!string.IsNullOrWhiteSpace(status)) q["status"] = status;
                q["page"] = page.ToString();
                q["pageSize"] = pageSize.ToString();

                var url = "/pharmacy/prescriptions" + "?" + q.ToString();
                var res = await _api.GetAsync<HMS.UI.Models.PagedResult<HMS.UI.Models.Pharmacy.PrescriptionViewModel>>(url);
                var model = res ?? new HMS.UI.Models.PagedResult<HMS.UI.Models.Pharmacy.PrescriptionViewModel> { Items = Array.Empty<HMS.UI.Models.Pharmacy.PrescriptionViewModel>(), Page = page, PageSize = pageSize, TotalCount = 0 };
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(new HMS.UI.Models.PagedResult<HMS.UI.Models.Pharmacy.PrescriptionViewModel>());
            }
        }

        [HMS.UI.Security.HasPermission("pharmacy.create")]
        public async Task<IActionResult> CreatePrescription()
        {
            // load drugs for selection
            try
            {
                var items = await _api.GetAsync<HMS.UI.Models.Pharmacy.InventoryItemViewModel[]>("/pharmacy/inventory");
                var vm = new HMS.UI.Models.Pharmacy.CreatePrescriptionViewModel { AvailableInventoryItems = items ?? Array.Empty<HMS.UI.Models.Pharmacy.InventoryItemViewModel>() };
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(new HMS.UI.Models.Pharmacy.CreatePrescriptionViewModel { AvailableInventoryItems = Array.Empty<HMS.UI.Models.Pharmacy.InventoryItemViewModel>() });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("pharmacy.create")]
        public async Task<IActionResult> CreatePrescription(HMS.UI.Models.Pharmacy.CreatePrescriptionViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            try
            {
                var payload = new
                {
                    PatientId = model.PatientId,
                    VisitId = model.VisitId,
                    Items = model.Items?.Where(i => i.InventoryItemId != Guid.Empty && i.Quantity > 0).Select(i => new { inventoryItemId = i.InventoryItemId, quantity = i.Quantity }).ToArray() ?? Array.Empty<object>()
                };

                var created = await _api.PostAsync<HMS.UI.Models.Pharmacy.PrescriptionViewModel>("/pharmacy/prescriptions", payload);
                TempData["Success"] = "Prescription created";
                return RedirectToAction("Prescriptions");
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(model);
            }
        }

        public async Task<IActionResult> PrescriptionDetails(Guid id)
        {
            try
            {
                var p = await _api.GetAsync<HMS.UI.Models.Pharmacy.PrescriptionViewModel>($"/pharmacy/prescriptions/{id}");
                if (p == null) return NotFound();
                return View(p);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Prescriptions");
            }
        }

        [HMS.UI.Security.HasPermission("pharmacy.create")]
        public async Task<IActionResult> EditPrescription(Guid id)
        {
            try
            {
                var p = await _api.GetAsync<HMS.UI.Models.Pharmacy.PrescriptionViewModel>($"/pharmacy/prescriptions/{id}");
                if (p == null) return NotFound();
                var items = await _api.GetAsync<HMS.UI.Models.Pharmacy.InventoryItemViewModel[]>("/pharmacy/inventory");
                var vm = new HMS.UI.Models.Pharmacy.EditPrescriptionViewModel { Prescription = p, AvailableInventoryItems = items ?? Array.Empty<HMS.UI.Models.Pharmacy.InventoryItemViewModel>() };
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Prescriptions");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("pharmacy.create")]
        public async Task<IActionResult> UpdatePrescription(Guid id, Guid patientId, Guid? visitId)
        {
            try
            {
                var payload = new { PatientId = patientId, VisitId = visitId };
                var resp = await _api.PutRawAsync($"/pharmacy/prescriptions/{id}", payload);
                if (!resp.IsSuccessStatusCode)
                {
                    TempData["Error"] = await resp.Content.ReadAsStringAsync();
                }
                else TempData["Success"] = "Prescription updated";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("PrescriptionDetails", new { id = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("pharmacy.create")]
        public async Task<IActionResult> UpdatePrescriptionItems(Guid id, Guid[] inventoryItemIds, int[] quantities, bool allowIfDispensed = false)
        {
            try
            {
                var items = new object[0];
                if (inventoryItemIds != null && inventoryItemIds.Length > 0)
                {
                    var list = new System.Collections.Generic.List<object>();
                    for (int i = 0; i < inventoryItemIds.Length && i < quantities.Length; i++)
                    {
                        if (inventoryItemIds[i] == Guid.Empty) continue;
                        if (quantities[i] <= 0) continue;
                        list.Add(new { inventoryItemId = inventoryItemIds[i], quantity = quantities[i] });
                    }
                    items = list.ToArray();
                }

                var payload = new { Items = items, AllowIfDispensed = allowIfDispensed };
                var resp = await _api.PutRawAsync($"/pharmacy/prescriptions/{id}/items", payload);
                if (!resp.IsSuccessStatusCode)
                {
                    TempData["Error"] = await resp.Content.ReadAsStringAsync();
                }
                else TempData["Success"] = "Prescription items updated";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("PrescriptionDetails", new { id = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("pharmacy.dispense")]
        public async Task<IActionResult> Dispense(Guid prescriptionId, Guid itemId, int quantity, bool allowOnCredit = false, string? reason = null)
        {
            try
            {
                var payload = new { PrescriptionItemId = itemId, Quantity = quantity, AllowOnCredit = allowOnCredit, CreditReason = reason };
                var resp = await _api.PostRawAsync("/pharmacy/dispense", payload);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    TempData["Error"] = string.IsNullOrWhiteSpace(body) ? $"API Error: {(int)resp.StatusCode} {resp.ReasonPhrase}" : body;
                }
                else TempData["Success"] = "Dispensed";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("PrescriptionDetails", new { id = prescriptionId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("pharmacy.dispense")]
        public async Task<IActionResult> AddPrescriptionNote(Guid prescriptionId, Guid itemId, string note)
        {
            try
            {
                var resp = await _api.PostRawAsync($"/pharmacy/prescriptions/{prescriptionId}/items/{itemId}/notes", note);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    TempData["Error"] = string.IsNullOrWhiteSpace(body) ? $"API Error: {(int)resp.StatusCode} {resp.ReasonPhrase}" : body;
                }
                else TempData["Success"] = "Note added";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("PrescriptionDetails", new { id = prescriptionId });
        }

        // Pharmacy reports
        public async Task<IActionResult> Reports()
        {
            try
            {
                var shortages = await _api.GetAsync<HMS.UI.Models.Pharmacy.StockShortageViewModel[]>("/api/reports/pharmacy/shortages");
                var daily = await _api.GetAsync<HMS.UI.Models.Pharmacy.DailyDispenseViewModel[]>("/api/reports/pharmacy/daily-dispenses");
                var revenue = await _api.GetAsync<HMS.UI.Models.Pharmacy.InventoryRevenueViewModel[]>("/api/reports/pharmacy/revenue-per-inventory");
                var vm = new HMS.UI.Models.Pharmacy.PharmacyReportsViewModel { Shortages = shortages ?? Array.Empty<HMS.UI.Models.Pharmacy.StockShortageViewModel>(), Daily = daily ?? Array.Empty<HMS.UI.Models.Pharmacy.DailyDispenseViewModel>(), Revenue = revenue ?? Array.Empty<HMS.UI.Models.Pharmacy.InventoryRevenueViewModel>() };
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(new HMS.UI.Models.Pharmacy.PharmacyReportsViewModel());
            }
        }

        // Show create form
        [HMS.UI.Security.HasPermission("pharmacy.inventory.manage")]
        public IActionResult CreateInventory()
        {
            return View(new HMS.UI.Models.Pharmacy.CreateInventoryItemViewModel());
        }

        public async Task<IActionResult> Inventory(string? category = null)
        {
            try
            {
                var items = await _api.GetAsync<HMS.UI.Models.Pharmacy.InventoryItemViewModel[]>($"/pharmacy/inventory" + (string.IsNullOrWhiteSpace(category) ? string.Empty : $"?category={Uri.EscapeDataString(category)}"));
                return View(items ?? Array.Empty<HMS.UI.Models.Pharmacy.InventoryItemViewModel>());
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(Array.Empty<HMS.UI.Models.Pharmacy.InventoryItemViewModel>());
            }
        }

        public async Task<IActionResult> InventoryDetails(Guid id)
        {
            try
            {
                var it = await _api.GetAsync<HMS.UI.Models.Pharmacy.InventoryItemViewModel>($"/pharmacy/inventory/{id}");
                if (it == null) return NotFound();
                return View(it);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Inventory");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("pharmacy.inventory.manage")]
        public async Task<IActionResult> CreateInventory(HMS.UI.Models.Pharmacy.CreateInventoryItemViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            try
            {
                var payload = new
                {
                    Code = model.Code,
                    Name = model.Name,
                    Description = model.Description,
                    UnitPrice = model.UnitPrice,
                    Currency = model.Currency,
                    Stock = model.Stock,
                    Category = model.Category,
                    Unit = model.Unit
                };
                var resp = await _api.PostRawAsync("/pharmacy/inventory", payload);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    if (string.IsNullOrWhiteSpace(body))
                    {
                        TempData["Error"] = $"API Error: {(int)resp.StatusCode} {resp.ReasonPhrase}";
                    }
                    else
                    {
                        TempData["Error"] = body;
                    }

                    // include ApiClient debug info to help diagnose (development only)
                    try
                    {
                        var dbg = _api.GetLastDebug();
                        if (dbg != null) TempData["ApiDebug"] = System.Text.Json.JsonSerializer.Serialize(dbg, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    }
                    catch { }

                    return View(model);
                }

                TempData["Success"] = "Inventory item created";
                return RedirectToAction("Inventory");
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
        public async Task<IActionResult> UpdateInventory(Guid id, HMS.UI.Models.Pharmacy.UpdateInventoryItemViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            try
            {
                var payload = new
                {
                    Code = model.Code,
                    Name = model.Name,
                    Description = model.Description,
                    UnitPrice = model.UnitPrice,
                    Currency = model.Currency,
                    Category = model.Category,
                    Unit = model.Unit
                };
                var resp = await _api.PutRawAsync($"/pharmacy/inventory/{id}", payload);
                if (!resp.IsSuccessStatusCode)
                {
                    TempData["Error"] = await resp.Content.ReadAsStringAsync();
                    return View(model);
                }

                TempData["Success"] = "Inventory item updated";
                return RedirectToAction("InventoryDetails", new { id = id });
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
                var resp = await _api.DeleteRawAsync($"/pharmacy/inventory/{id}");
                if (!resp.IsSuccessStatusCode)
                {
                    TempData["Error"] = await resp.Content.ReadAsStringAsync();
                }
                else
                {
                    TempData["Success"] = "Deleted";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("Inventory");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("pharmacy.inventory.manage")]
        public async Task<IActionResult> AdjustStock(Guid id, int delta)
        {
            try
            {
                var resp = await _api.PostRawAsync($"/pharmacy/inventory/{id}/adjust-stock", new { Delta = delta });
                if (!resp.IsSuccessStatusCode)
                {
                    TempData["Error"] = await resp.Content.ReadAsStringAsync();
                }
                else TempData["Success"] = "Stock adjusted";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("InventoryDetails", new { id = id });
        }
    }
}
