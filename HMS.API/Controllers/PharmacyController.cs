using System;
using System.Threading.Tasks;
using HMS.API.Application.Pharmacy;
using HMS.API.Application.Pharmacy.DTOs;
using HMS.API.Security;
using Microsoft.AspNetCore.Mvc;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PharmacyController : ControllerBase
    {
        private readonly IPharmacyService _pharmacy;

        public PharmacyController(IPharmacyService pharmacy)
        {
            _pharmacy = pharmacy;
        }

        // Drug endpoints removed. Use /pharmacy/inventory for medication management.

        [HttpPost("prescriptions")]
        [HasPermission("pharmacy.create")]
        public async Task<ActionResult<PrescriptionDto>> CreatePrescription([FromBody] CreatePrescriptionRequest req)
        {
            try
            {
                var p = await _pharmacy.CreatePrescriptionAsync(req);
                return CreatedAtAction(nameof(GetPrescription), new { id = p.Id }, p);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("prescriptions/{id}")]
        [HasPermission("pharmacy.view")]
        public async Task<ActionResult<PrescriptionDto>> GetPrescription(Guid id)
        {
            var p = await _pharmacy.GetPrescriptionAsync(id);
            if (p == null) return NotFound();
            return Ok(p);
        }

        [HttpGet("prescriptions")]
        [HasPermission("pharmacy.view")]
        public async Task<ActionResult> ListPrescriptions([FromQuery] Guid? patientId, [FromQuery] Guid? visitId, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var res = await _pharmacy.ListPrescriptionsAsync(patientId, visitId, status, page, pageSize);
            return Ok(res);
        }

        [HttpPost("dispense")]
        [HasPermission("pharmacy.dispense")]
        public async Task<ActionResult<DispenseDto>> Dispense([FromBody] DispenseRequest req)
        {
            try
            {
                var d = await _pharmacy.DispenseAsync(req);
                return Ok(d);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("prescriptions/{prescriptionId}/items/{itemId}/notes")]
        [HasPermission("pharmacy.dispense")]
        public async Task<ActionResult> AddNote(Guid prescriptionId, Guid itemId, [FromBody] string note)
        {
            try
            {
                await _pharmacy.AddNoteAsync(prescriptionId, itemId, note);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("prescriptions/{prescriptionId}/items/{itemId}/reconcile")]
        [HasPermission("pharmacy.dispense")]
        public async Task<ActionResult> ReconcileItem(Guid prescriptionId, Guid itemId, [FromBody] ReconcilePrescriptionItemRequest request)
        {
            try
            {
                await _pharmacy.ReconcilePrescriptionItemAsync(prescriptionId, itemId, request);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Update prescription header/details
        [HttpPut("prescriptions/{id}")]
        [HasPermission("pharmacy.create")]
        public async Task<ActionResult> UpdatePrescription(Guid id, [FromBody] UpdatePrescriptionRequest req)
        {
            try
            {
                await _pharmacy.UpdatePrescriptionAsync(id, req.PatientId, req.VisitId);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Replace/Update prescription items
        [HttpPut("prescriptions/{id}/items")]
        [HasPermission("pharmacy.create")]
        public async Task<ActionResult> UpdatePrescriptionItems(Guid id, [FromBody] UpdatePrescriptionItemsRequest req)
        {
            try
            {
                await _pharmacy.UpdatePrescriptionItemsAsync(id, req.Items, req.AllowIfDispensed);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    public class UpdatePrescriptionRequest { public Guid PatientId { get; set; } public Guid? VisitId { get; set; } }
    public class UpdatePrescriptionItemsRequest { public System.Collections.Generic.List<HMS.API.Application.Pharmacy.DTOs.CreatePrescriptionItem> Items { get; set; } = new(); public bool AllowIfDispensed { get; set; } = false; }
}
