using System;
using System.Threading.Tasks;
using System.Linq;
using HMS.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace HMS.UI.Controllers
{
    [HMS.UI.Security.HasPermission("payments.view")]
    public class PaymentsController : Controller
    {
        private readonly ApiClient _api;
        public PaymentsController(ApiClient api) { _api = api; }

        public IActionResult Index()
        {
            //return RedirectToAction(nameof(Payments));
            return View();
        }

        public async Task<IActionResult> Payments(Guid? invoiceId = null, Guid? patientId = null, int page = 1, int pageSize = 20)
        {
            try
            {
                var q = System.Web.HttpUtility.ParseQueryString(string.Empty);
                if (invoiceId.HasValue) q["invoiceId"] = invoiceId.Value.ToString();
                if (patientId.HasValue) q["patientId"] = patientId.Value.ToString();
                q["page"] = page.ToString();
                q["pageSize"] = pageSize.ToString();

                // Use billing invoice payments endpoint (invoice payment records) so UI shows recorded invoice payments
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

        public async Task<IActionResult> Details(Guid id)
        {
            try
            {
                var p = await _api.GetAsync<HMS.UI.Models.Payments.PaymentViewModel>($"/payments/{id}");
                if (p == null)
                {
                    // if payment not found, maybe this id is an invoice id - redirect to invoice details if present
                    try
                    {
                        var inv = await _api.GetAsync<HMS.UI.Models.Billing.InvoiceViewModel>($"/billing/{id}");
                        if (inv != null) return RedirectToAction("Details", "Billing", new { id = id });
                    }
                    catch (Exception ex)
                    {
                        // non-fatal: invoice lookup failed while handling missing payment id
                        // use debug-level to reduce noise in production logs
                        var logger = HttpContext.RequestServices.GetService(typeof(Microsoft.Extensions.Logging.ILogger<PaymentsController>)) as Microsoft.Extensions.Logging.ILogger;
                        logger?.LogDebug(ex, "Failed to resolve invoice {Id} while resolving payment details", id);
                    }

                    return NotFound();
                }

                return View(p);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Payments");
            }
        }

        public async Task<IActionResult> Receipt(Guid id)
        {
            try
            {
                var vm = await _api.GetAsync<HMS.UI.Models.Payments.ReceiptViewModel>($"/payments/{id}/receipt");
                if (vm == null) return NotFound();
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Payments");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Refund(Guid id, decimal amount, string reason)
        {
            try
            {
                var payload = new { Amount = amount, Reason = reason };
                var resp = await _api.PostRawAsync($"/payments/{id}/refund", payload);
                if (!resp.IsSuccessStatusCode)
                {
                    TempData["Error"] = await resp.Content.ReadAsStringAsync();
                }
                else TempData["Success"] = "Refund created.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id = id });
        }

        public async Task<IActionResult> Refunds(Guid? paymentId = null, Guid? patientId = null, int page = 1, int pageSize = 20)
        {
            try
            {
                var q = System.Web.HttpUtility.ParseQueryString(string.Empty);
                if (paymentId.HasValue) q["paymentId"] = paymentId.Value.ToString();
                if (patientId.HasValue) q["patientId"] = patientId.Value.ToString();
                q["page"] = page.ToString();
                q["pageSize"] = pageSize.ToString();

                var url = "/payments/refunds?" + q.ToString();
                var res = await _api.GetAsync<HMS.UI.Models.PagedResult<HMS.UI.Models.Payments.RefundViewModel>>(url);
                if (res == null)
                {
                    TempData["Error"] = "Unable to load refunds.";
                    return RedirectToAction("Payments");
                }

                return View(res);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Payments");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReverseRefund(Guid id, string reason)
        {
            try
            {
                var payload = new { Reason = reason };
                var resp = await _api.PostRawAsync($"/payments/refunds/{id}/reverse", payload);
                if (!resp.IsSuccessStatusCode)
                {
                    TempData["Error"] = await resp.Content.ReadAsStringAsync();
                }
                else TempData["Success"] = "Refund reversed.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Refunds");
        }
    }
}
