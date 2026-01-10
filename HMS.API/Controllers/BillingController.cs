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

        public BillingController(IBillingService billing)
        {
            _billing = billing;
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
    }
}