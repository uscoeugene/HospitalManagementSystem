using System;
using System.Threading.Tasks;
using HMS.API.Application.Payments;
using HMS.API.Application.Payments.DTOs;
using HMS.API.Security;
using Microsoft.AspNetCore.Mvc;
using HMS.API.Application.Common;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _payments;

        public PaymentsController(IPaymentService payments)
        {
            _payments = payments;
        }

        [HttpPost]
        [HasPermission("payments.create")]
        public async Task<ActionResult<PaymentDto>> Create([FromBody] CreatePaymentRequest request)
        {
            try
            {
                var p = await _payments.CreatePaymentAsync(request);
                return CreatedAtAction(nameof(Get), new { id = p.Id }, p);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        [HasPermission("payments.view")]
        public async Task<ActionResult<PaymentDto>> Get(Guid id)
        {
            var p = await _payments.GetPaymentAsync(id);
            if (p == null) return NotFound();
            return Ok(p);
        }

        [HttpGet("{id}/receipt")]
        [HasPermission("payments.view")]
        public async Task<ActionResult<ReceiptDto>> Receipt(Guid id)
        {
            try
            {
                var r = await _payments.GenerateReceiptAsync(id);
                return Ok(r);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet]
        [HasPermission("payments.view")]
        public async Task<ActionResult> List([FromQuery] Guid? invoiceId, [FromQuery] Guid? patientId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var res = await _payments.ListPaymentsAsync(invoiceId, patientId, page, pageSize);
            return Ok(res);
        }

        [HttpPost("{id}/refund")]
        [HasPermission("payments.create")]
        public async Task<ActionResult<RefundDto>> Refund(Guid id, [FromBody] RefundRequest request)
        {
            try
            {
                var r = await _payments.CreateRefundAsync(id, request);
                return Ok(r);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("refunds")]
        [HasPermission("payments.view")]
        public async Task<ActionResult> ListRefunds([FromQuery] Guid? paymentId, [FromQuery] Guid? patientId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var res = await _payments.ListRefundsAsync(paymentId, patientId, page, pageSize);
            return Ok(res);
        }

        [HttpPost("refunds/{id}/reverse")]
        [HasPermission("payments.create")]
        public async Task<ActionResult<RefundReversalDto>> ReverseRefund(Guid id, [FromBody] RefundReversalRequest request)
        {
            try
            {
                var r = await _payments.CreateRefundReversalAsync(id, request);
                return Ok(r);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}