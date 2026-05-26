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

        public async Task<IActionResult> Index(Guid? patientId = null, Guid? visitId = null, int page = 1, int pageSize = 20)
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
