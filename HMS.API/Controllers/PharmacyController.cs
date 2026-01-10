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

        [HttpGet("drugs")]
        [HasPermission("pharmacy.view")]
        public async Task<ActionResult> ListDrugs()
        {
            var d = await _pharmacy.ListDrugsAsync();
            return Ok(d);
        }

        [HttpPost("drugs")]
        [HasPermission("pharmacy.manage")]
        public async Task<ActionResult<DrugDto>> CreateDrug([FromBody] DrugDto dto)
        {
            var res = await _pharmacy.CreateDrugAsync(dto);
            return CreatedAtAction(nameof(ListDrugs), new { id = res.Id }, res);
        }

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
        public async Task<ActionResult> ListPrescriptions([FromQuery] Guid? patientId, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var res = await _pharmacy.ListPrescriptionsAsync(patientId, status, page, pageSize);
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
    }
}