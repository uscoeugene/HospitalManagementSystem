using System;
using System.Threading.Tasks;
using HMS.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace HMS.UI.Controllers
{
    [HMS.UI.Security.HasPermission("billing.view")]
    public class BillingController : Controller
    {
        private readonly ApiClient _api;
        public BillingController(ApiClient api) { _api = api; }

        public IActionResult Index()
        {
            return View();
        }

        // List payments (JSON) or simple view later
        public async Task<IActionResult> Payments(Guid? invoiceId = null, Guid? patientId = null, int page = 1, int pageSize = 20)
        {
            try
            {
                var q = System.Web.HttpUtility.ParseQueryString(string.Empty);
                if (invoiceId.HasValue) q["invoiceId"] = invoiceId.Value.ToString();
                if (patientId.HasValue) q["patientId"] = patientId.Value.ToString();
                q["page"] = page.ToString();
                q["pageSize"] = pageSize.ToString();

                var url = "/billing/payments?" + q.ToString();
                var res = await _api.GetAsync<HMS.UI.Models.PagedResult<HMS.UI.Models.Billing.InvoicePaymentViewModel>>(url);
                if (res == null)
                {
                    TempData["Error"] = "Unable to load payments.";
                    return RedirectToAction("Index");
                }

                return View(res);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        // Export invoice - redirect to API file endpoint so browser downloads it
        public IActionResult ExportInvoice(Guid id)
        {
            return Redirect($"/billing/{id}/export");
        }

        // Export invoices (filtered) - redirect to API export
        public IActionResult ExportInvoices(Guid? patientId = null, Guid? visitId = null, string? status = null)
        {
            var q = System.Web.HttpUtility.ParseQueryString(string.Empty);
            if (patientId.HasValue) q["patientId"] = patientId.Value.ToString();
            if (visitId.HasValue) q["visitId"] = visitId.Value.ToString();
            if (!string.IsNullOrWhiteSpace(status)) q["status"] = status;
            return Redirect($"/billing/export?{q}");
        }

        // Debts list
        public async Task<IActionResult> Debts(Guid? invoiceId = null, Guid? patientId = null, bool unresolvedOnly = true, int page = 1, int pageSize = 20)
        {
            try
            {
                var q = System.Web.HttpUtility.ParseQueryString(string.Empty);
                if (invoiceId.HasValue) q["invoiceId"] = invoiceId.Value.ToString();
                if (patientId.HasValue) q["patientId"] = patientId.Value.ToString();
                q["unresolvedOnly"] = unresolvedOnly.ToString();
                q["page"] = page.ToString();
                q["pageSize"] = pageSize.ToString();

                var url = "/billing/debts?" + q.ToString();
                var res = await _api.GetAsync<HMS.UI.Models.PagedResult<HMS.UI.Models.Billing.DebtViewModel>>(url);
                if (res == null)
                {
                    TempData["Error"] = "Unable to load debts.";
                    return RedirectToAction("Index");
                }

                // return an HTML view so users can manage debts from UI
                return View(res);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResolveDebt(Guid id)
        {
            try
            {
                var resp = await _api.PostRawAsync($"/billing/debts/{id}/resolve", null);
                if (!resp.IsSuccessStatusCode)
                {
                    TempData["Error"] = await resp.Content.ReadAsStringAsync();
                }
                else TempData["Success"] = "Debt resolved.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayDebt(Guid id, decimal amount, string? externalReference)
        {
            try
            {
                var payload = new { Amount = amount, ExternalReference = externalReference };
                var resp = await _api.PostRawAsync($"/billing/debts/{id}/pay", payload);
                if (!resp.IsSuccessStatusCode)
                {
                    TempData["Error"] = await resp.Content.ReadAsStringAsync();
                }
                else TempData["Success"] = "Debt payment recorded.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayDebtsBatch([FromBody] HMS.UI.Models.Billing.BatchPayDebtRequest[] reqs)
        {
            try
            {
                var resp = await _api.PostRawAsync($"/billing/debts/pay-batch", reqs);
                if (!resp.IsSuccessStatusCode)
                {
                    return StatusCode((int)resp.StatusCode, await resp.Content.ReadAsStringAsync());
                }

                var result = await resp.Content.ReadAsStringAsync();
                return Content(result, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        public async Task<IActionResult> DebtAging(int daysBucket = 30)
        {
            try
            {
                var res = await _api.GetAsync<HMS.UI.Models.Billing.DebtAgingViewModel[]>($"/billing/debts/aging?daysBucket={daysBucket}");
                return Json(res);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        public async Task<IActionResult> OutstandingByPatient()
        {
            try
            {
                var res = await _api.GetAsync<HMS.UI.Models.Billing.OutstandingByPatientViewModel[]>("/billing/debts/outstanding-by-patient");
                return Json(res);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NotifyOverdue(int minAgeDays = 30)
        {
            try
            {
                var payload = new { MinAgeDays = minAgeDays };
                var resp = await _api.PostRawAsync("/billing/debts/notify-overdue", payload);
                if (!resp.IsSuccessStatusCode)
                {
                    TempData["Error"] = await resp.Content.ReadAsStringAsync();
                }
                else
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    TempData["Success"] = "Overdue notifications queued.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index");
        }
        public async Task<IActionResult> Invoices(Guid? patientId = null, Guid? visitId = null, int page = 1, int pageSize = 20)
        {
            try
            {
                var q = System.Web.HttpUtility.ParseQueryString(string.Empty);
                if (patientId.HasValue) q["patientId"] = patientId.Value.ToString();
                if (visitId.HasValue) q["visitId"] = visitId.Value.ToString();
                q["page"] = page.ToString();
                q["pageSize"] = pageSize.ToString();

                var url = "/billing" + "?" + q.ToString();

                var pageRes = await _api.GetAsync<HMS.UI.Models.PagedResult<HMS.UI.Models.Billing.InvoiceViewModel>>(url);
                var model = pageRes ?? new HMS.UI.Models.PagedResult<HMS.UI.Models.Billing.InvoiceViewModel> { Items = Array.Empty<HMS.UI.Models.Billing.InvoiceViewModel>(), Page = page, PageSize = pageSize, TotalCount = 0 };
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(new HMS.UI.Models.PagedResult<HMS.UI.Models.Billing.InvoiceViewModel>());
            }
        }

        public async Task<IActionResult> Details(Guid id)
        {
            try
            {
                var inv = await _api.GetAsync<HMS.UI.Models.Billing.InvoiceViewModel>($"/billing/{id}");
                if (inv == null) return NotFound();
                return View(inv);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(Guid id, decimal amount, string? externalReference)
        {
            try
            {
                var payload = new { Amount = amount, ExternalReference = externalReference };
                var resp = await _api.PostRawAsync($"/billing/{id}/payments", payload);
                if (!resp.IsSuccessStatusCode)
                {
                    TempData["Error"] = await resp.Content.ReadAsStringAsync();
                }
                else TempData["Success"] = "Payment applied";
                return RedirectToAction("Details", new { id = id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Details", new { id = id });
            }
        }
    }
}
