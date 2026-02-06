using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HMS.API.Application.Billing;
using HMS.API.Application.Billing.DTOs;
using HMS.API.Security;
using Microsoft.AspNetCore.Mvc;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BillingController : ControllerBase
    {
        private readonly IBillingService _billing;
        private readonly HMS.API.Application.Common.INotificationService _notifier;

        public BillingController(IBillingService billing, HMS.API.Application.Common.INotificationService notifier)
        {
            _billing = billing;
            _notifier = notifier;
        }

        [HttpPost]
        [HasPermission("billing.create")]
        public async Task<ActionResult<InvoiceDto>> Create([FromBody] CreateInvoiceRequest request)
        {
            var inv = await _billing.CreateInvoiceAsync(request);
            return CreatedAtAction(nameof(Get), new { id = inv.Id }, inv);
        }

        [HttpGet("{id}")]
        [HasPermission("billing.view")]
        public async Task<ActionResult<InvoiceDto>> Get(Guid id)
        {
            var inv = await _billing.GetInvoiceAsync(id);
            if (inv == null) return NotFound();
            return Ok(inv);
        }

        [HttpGet]
        [HasPermission("billing.view")]
        public async Task<ActionResult> List([FromQuery] Guid? patientId, [FromQuery] Guid? visitId, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var res = await _billing.ListInvoicesAsync(patientId, visitId, status, page, pageSize);
            return Ok(res);
        }

        [HttpGet("payments")]
        [HasPermission("billing.view")]
        public async Task<ActionResult> ListPayments([FromQuery] Guid? invoiceId, [FromQuery] Guid? patientId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var res = await _billing.ListPaymentsAsync(invoiceId, patientId, page, pageSize);
            return Ok(res);
        }

        [HttpGet("{id}/export")]
        [HasPermission("billing.export")]
        public async Task ExportInvoice(Guid id)
        {
            var inv = await _billing.GetInvoiceAsync(id);
            if (inv == null) { Response.StatusCode = 404; return; }

            Response.Headers.Add("Content-Disposition", $"attachment; filename=invoice-{inv.InvoiceNumber}.csv");
            Response.ContentType = "text/csv";

            await using var writer = new StreamWriter(Response.Body, Encoding.UTF8);
            await writer.WriteLineAsync("InvoiceNumber,PatientId,VisitId,Status,Currency,TotalAmount,AmountPaid");
            await writer.WriteLineAsync($"{inv.InvoiceNumber},{inv.PatientId},{inv.VisitId},{inv.Status},{inv.Currency},{inv.TotalAmount},{inv.AmountPaid}");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("ItemDescription,UnitPrice,Quantity,LineTotal");
            foreach (var it in inv.Items)
            {
                var desc = it.Description?.Replace("\"", "\"\"") ?? string.Empty;
                var line = $"\"{desc}\",{it.UnitPrice},{it.Quantity},{it.LineTotal}";
                await writer.WriteLineAsync(line);
            }

            await writer.FlushAsync();
        }

        [HttpGet("export")]
        [HasPermission("billing.export")]
        public async Task ExportInvoices([FromQuery] Guid? patientId, [FromQuery] Guid? visitId, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        {
            var res = await _billing.ListInvoicesAsync(patientId, visitId, status, page, pageSize);

            Response.Headers.Add("Content-Disposition", $"attachment; filename=invoices-export-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv");
            Response.ContentType = "text/csv";

            await using var writer = new StreamWriter(Response.Body, Encoding.UTF8);
            await writer.WriteLineAsync("InvoiceNumber,PatientId,VisitId,Status,Currency,TotalAmount,AmountPaid");
            foreach (var inv in res.Items)
            {
                await writer.WriteLineAsync($"{inv.InvoiceNumber},{inv.PatientId},{inv.VisitId},{inv.Status},{inv.Currency},{inv.TotalAmount},{inv.AmountPaid}");
            }

            await writer.FlushAsync();
        }

        [HttpPost("{id}/payments")]
        [HasPermission("billing.applypayment")]
        public async Task<ActionResult<InvoiceDto>> ApplyPayment(Guid id, [FromBody] ApplyPaymentRequest request)
        {
            try
            {
                var inv = await _billing.ApplyPaymentAsync(id, request);
                return Ok(inv);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("debts")]
        [HasPermission("billing.view")]
        public async Task<ActionResult> ListDebts([FromQuery] Guid? invoiceId, [FromQuery] Guid? patientId, [FromQuery] bool unresolvedOnly = true, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var res = await _billing.ListDebtsPagedAsync(invoiceId, patientId, unresolvedOnly, page, pageSize);
            return Ok(res);
        }

        [HttpPost("debts/{id}/resolve")]
        [HasPermission("billing.manage")]
        public async Task<ActionResult> ResolveDebt(Guid id)
        {
            try
            {
                await _billing.ResolveDebtAsync(id);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("debts/{id}/pay")]
        [HasPermission("billing.manage")]
        public async Task<ActionResult> PayDebt(Guid id, [FromBody] PaymentToDebtRequest req)
        {
            try
            {
                await _billing.PayDebtAsync(id, req.Amount, req.ExternalReference);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("debts/pay-batch")]
        [HasPermission("billing.manage")]
        public async Task<ActionResult> PayDebtsBatch([FromBody] BatchPayDebtRequest[] reqs)
        {
            var results = await _billing.PayMultipleDebtsAsync(reqs);
            return Ok(results);
        }

        [HttpGet("debts/aging")]
        [HasPermission("billing.view")]
        public async Task<ActionResult> DebtAging([FromQuery] int daysBucket = 30)
        {
            var res = await _billing.GetDebtAgingReportAsync(daysBucket);
            return Ok(res);
        }

        [HttpGet("debts/outstanding-by-patient")]
        [HasPermission("billing.view")]
        public async Task<ActionResult> OutstandingByPatient()
        {
            var res = await _billing.GetOutstandingByPatientAsync();
            return Ok(res);
        }

        [HttpPost("debts/notify-overdue")]
        [HasPermission("billing.manage")]
        public async Task<ActionResult> NotifyOverdue([FromBody] NotifyOverdueRequest req)
        {
            // scan debts older than threshold and notify via notification service
            var now = DateTimeOffset.UtcNow;
            var debts = await _billing.ListDebtsAsync(null, true);
            var items = debts.Where(d => (now - d.CreatedAt).TotalDays >= req.MinAgeDays).ToArray();
            foreach (var d in items)
            {
                await _notifier.NotifyAsync("email", new { patientId = d.InvoiceId, debtId = d.Id, amount = d.AmountOwed });
                // tracking notifications is done in DB within billing service if needed
            }

            return Ok(new { notified = items.Length });
        }
    }

    public class PaymentToDebtRequest
    {
        public decimal Amount { get; set; }
        public string? ExternalReference { get; set; }
    }

    public class NotifyOverdueRequest
    {
        public int MinAgeDays { get; set; } = 30;
    }
}