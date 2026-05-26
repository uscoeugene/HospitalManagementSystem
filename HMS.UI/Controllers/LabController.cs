using HMS.UI.Services;
using Microsoft.AspNetCore.Mvc;
using HMS.UI.Models;
using HMS.UI.Models.Lab;

namespace HMS.UI.Controllers
{
    [HMS.UI.Security.HasPermission("lab.view")]
    public class LabController : Controller
    {
        private readonly ApiClient _api;

        public LabController(ApiClient api) { _api = api; }

        public async Task<IActionResult> Index()
        {
            try
            {
                var tests = await _api.GetAsync<LabTestViewModel[]>("/lab/tests");
                return View(tests ?? Array.Empty<LabTestViewModel>());
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(Array.Empty<LabTestViewModel>());
            }
        }

        [HMS.UI.Security.HasPermission("lab.manage")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("lab.manage")]
        public async Task<IActionResult> Create(LabTestViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            try
            {
                await _api.PostAsync<object>("/lab/tests", model);
                TempData["Success"] = "Lab test created";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(model);
            }
        }

        [HMS.UI.Security.HasPermission("lab.request")]
        public async Task<IActionResult> Request(Guid patientId, Guid? visitId)
        {
            try
            {
                var tests = await _api.GetAsync<LabTestViewModel[]>("/lab/tests");
                var vm = new LabRequestCreateViewModel { PatientId = patientId, VisitId = visitId, AvailableTests = tests ?? Array.Empty<LabTestViewModel>() };

                // attempt to fetch patient info for header display
                try
                {
                    var p = await _api.GetAsync<HMS.UI.Models.PatientDetailsViewModel>($"/patients/{patientId}");
                    vm.Patient = p;
                }
                catch { vm.Patient = null; }
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index", "Patients");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("lab.request")]
        public async Task<IActionResult> Request(LabRequestCreateViewModel model)
        {
            if (model == null || model.SelectedTestIds == null || model.SelectedTestIds.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "Select at least one test");
                TempData["Error"] = "Select at least one test";
                try { model.AvailableTests = await _api.GetAsync<LabTestViewModel[]>("/lab/tests") ?? Array.Empty<LabTestViewModel>(); } catch { model.AvailableTests = Array.Empty<LabTestViewModel>(); }
                try { model.Patient = await _api.GetAsync<HMS.UI.Models.PatientDetailsViewModel>($"/patients/{model.PatientId}"); } catch { model.Patient = null; }
                return View(model);
            }

            try
            {
                var payload = new
                {
                    PatientId = model.PatientId,
                    VisitId = model.VisitId,
                    Items = model.SelectedTestIds.Select(id => new { LabTestId = id }).ToArray(),
                    AllowOnCredit = model.AllowOnCredit,
                    CreditReason = model.CreditReason
                };

                var resp = await _api.PostRawAsync("/lab/requests", payload);
                if (!resp.IsSuccessStatusCode)
                {
                    var b = await resp.Content.ReadAsStringAsync();
                    ModelState.AddModelError(string.Empty, "Failed to create lab request: " + b);
                    TempData["Error"] = "Failed to create lab request: " + b;
                    try { model.AvailableTests = await _api.GetAsync<LabTestViewModel[]>("/lab/tests") ?? Array.Empty<LabTestViewModel>(); } catch { model.AvailableTests = Array.Empty<LabTestViewModel>(); }
                    try { model.Patient = await _api.GetAsync<HMS.UI.Models.PatientDetailsViewModel>($"/patients/{model.PatientId}"); } catch { model.Patient = null; }
                    return View(model);
                }

                // Try to find the invoice created for this visit (API creates invoice when lab request created)
                Guid? invoiceId = null;
                try
                {
                    if (model.VisitId.HasValue)
                    {
                        var list = await _api.GetAsync<System.Text.Json.JsonElement>($"/billing?visitId={model.VisitId.Value}&page=1&pageSize=5");
                        if (list.ValueKind == System.Text.Json.JsonValueKind.Object && list.TryGetProperty("items", out var items) && items.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            var first = items.EnumerateArray().FirstOrDefault();
                            if (first.ValueKind == System.Text.Json.JsonValueKind.Object && first.TryGetProperty("id", out var iid) && iid.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                if (Guid.TryParse(iid.GetString(), out var gid)) invoiceId = gid;
                            }
                        }
                    }
                }
                catch { }

                TempData["Success"] = "Lab request created";
                if (invoiceId.HasValue) TempData["ShowInvoiceId"] = invoiceId.Value.ToString();

                if (model.VisitId.HasValue)
                {
                    return RedirectToAction("VisitDetails", "Patients", new { id = model.VisitId.Value });
                }
                else
                {
                    return RedirectToAction("Details", "Patients", new { id = model.PatientId });
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index", "Patients");
            }
        }

        [HMS.UI.Security.HasPermission("lab.view")]
        public async Task<IActionResult> Requests(Guid? patientId, Guid? visitId, int page = 1)
        {
            try
            {
                var q = $"/lab/requests?page={page}&pageSize=50";
                if (patientId.HasValue) q += $"&patientId={patientId.Value}";
                if (visitId.HasValue) q += $"&visitId={visitId.Value}";

                var pageRes = await _api.GetAsync<HMS.UI.Models.PagedResult<HMS.UI.Models.Lab.LabRequestViewModel>>(q);
                var list = pageRes?.Items ?? Array.Empty<HMS.UI.Models.Lab.LabRequestViewModel>();
                return View(pageRes ?? new HMS.UI.Models.PagedResult<HMS.UI.Models.Lab.LabRequestViewModel>());
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(new HMS.UI.Models.PagedResult<HMS.UI.Models.Lab.LabRequestViewModel>());
            }
        }

        [HMS.UI.Security.HasPermission("lab.view")]
        public async Task<IActionResult> Details(Guid id)
        {
            try
            {
                var vm = await _api.GetAsync<HMS.UI.Models.Lab.LabRequestViewModel>($"/lab/requests/{id}");
                if (vm == null) return NotFound();
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Requests");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("lab.process")]
        public async Task<IActionResult> UpdateResult(Guid requestId, Guid itemId, string? ResultValue, string? ResultUnit, string? ReferenceRange, string? AbnormalFlag, string? ResultNotes, bool Verify = false)
        {
            try
            {
                var payload = new
                {
                    ResultValue,
                    ResultUnit,
                    ReferenceRange,
                    AbnormalFlag,
                    ResultNotes,
                    Verify
                };

                var resp = await _api.PutRawAsync($"/lab/requests/{requestId}/items/{itemId}/result", payload);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    TempData["Error"] = "Failed to update result: " + body;
                }
                else
                {
                    TempData["Success"] = "Result updated";
                }

                return RedirectToAction("Details", new { id = requestId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Details", new { id = requestId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [HMS.UI.Security.HasPermission("lab.process")]
        public async Task<IActionResult> UploadResultAttachment(Guid requestId, Guid itemId, Microsoft.AspNetCore.Http.IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "File is required";
                return RedirectToAction("Details", new { id = requestId });
            }

            try
            {
                var headers = new System.Collections.Generic.Dictionary<string, string>();
                var res = await _api.PostFileAsync($"/lab/requests/{requestId}/items/{itemId}/result/attachment", file, null, headers);
                if (!res.IsSuccessStatusCode)
                {
                    var b = await res.Content.ReadAsStringAsync();
                    TempData["Error"] = "Failed to upload attachment: " + b;
                }
                else
                {
                    TempData["Success"] = "Attachment uploaded";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id = requestId });
        }
    }
}
