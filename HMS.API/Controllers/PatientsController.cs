using System;
using System.Threading.Tasks;
using HMS.API.Application.Patient;
using HMS.API.Application.Patient.DTOs;
using HMS.API.Application.Common;
using Microsoft.AspNetCore.Mvc;
using HMS.API.Security;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PatientsController : ControllerBase
    {
        private readonly IPatientService _service;

        public PatientsController(IPatientService service)
        {
            _service = service;
        }

        [HttpDelete("consultations/{id}")]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> DeleteConsultation(Guid id)
        {
            try
            {
                await _service.DeleteConsultationAsync(id);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(HMS.API.Application.Common.ApiResponse<object>.ForError("NOT_FOUND", ex.Message, 404));
            }
        }

        [HttpGet("visits/{visitId}/consultations")]
        [HasPermission("patients.view")]
        public async Task<IActionResult> ListConsultationsForVisit(Guid visitId)
        {
            var list = await _service.ListConsultationsForVisitAsync(visitId);
            return Ok(HMS.API.Application.Common.ApiResponse<HMS.API.Application.Patient.DTOs.ConsultationResponse[]>.ForSuccess(list, 200));
        }

        [HttpPost("{id}/visits/{visitId}/consultations")]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> AddConsultation(Guid id, Guid visitId, [FromBody] CreateConsultationRequest req)
        {
            try
            {
                if (req.VisitId != visitId) return BadRequest(HMS.API.Application.Common.ApiResponse<object>.ForError("INVALID_REQUEST", "VisitId mismatch", 400));
                var c = await _service.AddConsultationAsync(id, req);
                return CreatedAtAction(nameof(GetConsultation), new { id = c.Id }, HMS.API.Application.Common.ApiResponse<HMS.API.Application.Patient.DTOs.ConsultationResponse>.ForSuccess(c, 201));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(HMS.API.Application.Common.ApiResponse<object>.ForError("INVALID_OPERATION", ex.Message, 400));
            }
        }

        [HttpGet("consultations/{id}")]
        [HasPermission("patients.view")]
        public async Task<IActionResult> GetConsultation(Guid id)
        {
            var c = await _service.GetConsultationAsync(id);
            if (c == null) return NotFound();
            return Ok(HMS.API.Application.Common.ApiResponse<HMS.API.Application.Patient.DTOs.ConsultationResponse>.ForSuccess(c, 200));
        }

        [HttpPut("consultations/{id}")]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> UpdateConsultation(Guid id, [FromBody] CreateConsultationRequest req)
        {
            try
            {
                var c = await _service.UpdateConsultationAsync(id, req);
                return Ok(HMS.API.Application.Common.ApiResponse<HMS.API.Application.Patient.DTOs.ConsultationResponse>.ForSuccess(c, 200));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(HMS.API.Application.Common.ApiResponse<object>.ForError("INVALID_OPERATION", ex.Message, 400));
            }
        }

        [HttpPost("{id}/vitals")]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> AddVitalForPatient(Guid id, [FromBody] HMS.API.Application.Patient.DTOs.CreateVitalSignRequest req)
        {
            try
            {
                var v = await _service.AddVitalSignAsync(id, req);
                return CreatedAtAction(nameof(GetVitalSign), new { id = v.Id }, HMS.API.Application.Common.ApiResponse<HMS.API.Application.Patient.DTOs.VitalSignResponse>.ForSuccess(v, 201));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(HMS.API.Application.Common.ApiResponse<object>.ForError("INVALID_OPERATION", ex.Message, 400));
            }
        }

        [HttpGet("{id}/visits")]
        [HasPermission("patients.view")]
        public async Task<IActionResult> ListVisits(Guid id)
        {
            var visits = await _service.ListVisitsForPatientAsync(id);
            return Ok(HMS.API.Application.Common.ApiResponse<VisitResponse[]>.ForSuccess(visits, 200));
        }

        [HttpPost("{id}/visits/{visitId}/vitals")]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> AddVital(Guid id, Guid visitId, [FromBody] HMS.API.Application.Patient.DTOs.CreateVitalSignRequest req)
        {
            try
            {
                // enforce request.VisitId matches route visitId
                if (req.VisitId != visitId) return BadRequest(HMS.API.Application.Common.ApiResponse<object>.ForError("INVALID_REQUEST", "VisitId mismatch", 400));
                var v = await _service.AddVitalSignAsync(id, req);
                return CreatedAtAction(nameof(GetVitalSign), new { id = v.Id }, HMS.API.Application.Common.ApiResponse<HMS.API.Application.Patient.DTOs.VitalSignResponse>.ForSuccess(v, 201));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(HMS.API.Application.Common.ApiResponse<object>.ForError("INVALID_OPERATION", ex.Message, 400));
            }
        }

        [HttpGet("vitals/{id}")]
        [HasPermission("patients.view")]
        public async Task<IActionResult> GetVitalSign(Guid id)
        {
            var vs = await _service.GetVitalSignAsync(id);
            if (vs == null) return NotFound();
            return Ok(HMS.API.Application.Common.ApiResponse<HMS.API.Application.Patient.DTOs.VitalSignResponse>.ForSuccess(vs, 200));
        }

        [HttpPut("vitals/{id}")]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> UpdateVitalSign(Guid id, [FromBody] CreateVitalSignRequest req)
        {
            try
            {
                var vs = await _service.UpdateVitalSignAsync(id, req);
                return Ok(HMS.API.Application.Common.ApiResponse<VitalSignResponse>.ForSuccess(vs, 200));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(HMS.API.Application.Common.ApiResponse<object>.ForError("INVALID_OPERATION", ex.Message, 400));
            }
        }

        [HttpGet("visits/{visitId}/vitals")]
        [HasPermission("patients.view")]
        public async Task<IActionResult> ListVitalsForVisit(Guid visitId)
        {
            var list = await _service.ListVitalSignsForVisitAsync(visitId);
            return Ok(HMS.API.Application.Common.ApiResponse<HMS.API.Application.Patient.DTOs.VitalSignResponse[]>.ForSuccess(list, 200));
        }

        [HttpGet("visits/{id}")]
        [HasPermission("patients.view")]
        public async Task<IActionResult> GetVisit(Guid id)
        {
            var v = await _service.GetVisitAsync(id);
            if (v == null) return NotFound();
            return Ok(HMS.API.Application.Common.ApiResponse<VisitResponse>.ForSuccess(v, 200));
        }

        [HttpPost("{id}/visits")]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> CreateVisit(Guid id, [FromBody] AddVisitRequest request)
        {
            try
            {
                var v = await _service.AddVisitAsync(id, request);
                return CreatedAtAction(nameof(GetVisit), new { id = v.Id }, HMS.API.Application.Common.ApiResponse<VisitResponse>.ForSuccess(v, 201));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(HMS.API.Application.Common.ApiResponse<object>.ForError("INVALID_OPERATION", ex.Message, 400));
            }
        }

        [HttpPut("visits/{id}")]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> UpdateVisit(Guid id, [FromBody] AddVisitRequest request)
        {
            try
            {
                var v = await _service.UpdateVisitAsync(id, request);
                return Ok(HMS.API.Application.Common.ApiResponse<VisitResponse>.ForSuccess(v, 200));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(HMS.API.Application.Common.ApiResponse<object>.ForError("INVALID_OPERATION", ex.Message, 400));
            }
        }

        [HttpDelete("visits/{id}")]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> DeleteVisit(Guid id)
        {
            try
            {
                await _service.DeleteVisitAsync(id);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(HMS.API.Application.Common.ApiResponse<object>.ForError("INVALID_OPERATION", ex.Message, 400));
            }
        }

        [HttpPut("{id}")]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> Update(Guid id, [FromBody] RegisterPatientRequest request)
        {
            try
            {
                var existing = await _service.GetPatientAsync(id);
                if (existing == null) return NotFound();

                var updated = await _service.UpdatePatientAsync(id, request);
                return Ok(HMS.API.Application.Common.ApiResponse<PatientResponse>.ForSuccess(updated, 200));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(HMS.API.Application.Common.ApiResponse<object>.ForError("INVALID_OPERATION", ex.Message, 400));
            }
        }

        [HttpPost]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> Register([FromBody] RegisterPatientRequest request)
        {
            try
            {
                var resp = await _service.RegisterPatientAsync(request);
                var api = HMS.API.Application.Common.ApiResponse<PatientResponse>.ForSuccess(resp, 201);
                return CreatedAtAction(nameof(Get), new { id = resp.Id }, api);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(HMS.API.Application.Common.ApiResponse<object>.ForError("INVALID_OPERATION", ex.Message, 400));
            }
        }

        [HttpGet("{id}")]
        [HasPermission("patients.view")]
        public async Task<IActionResult> Get(Guid id)
        {
            var p = await _service.GetPatientAsync(id);
            if (p == null) return NotFound();
            return Ok(HMS.API.Application.Common.ApiResponse<PatientResponse>.ForSuccess(p, 200));
        }

        [HttpGet]
        [HasPermission("patients.view")]
        public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var res = await _service.ListPatientsAsync(q, page, pageSize);
            return Ok(HMS.API.Application.Common.ApiResponse<PagedResult<PatientResponse>>.ForSuccess(res, 200));
        }

        

        [HttpGet("possible-duplicates")]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> PossibleDuplicates([FromQuery] string q, [FromQuery] double threshold = 0.75, [FromQuery] int dobToleranceDays = 365, [FromQuery] int mrnPrefixLength = 4)
        {
            var res = await _service.FindPossibleDuplicatesAsync(q, threshold, dobToleranceDays, mrnPrefixLength);
            return Ok(HMS.API.Application.Common.ApiResponse<DuplicateCandidateDto[]>.ForSuccess(res, 200));
        }

        [HttpPost("{id}/merge")]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> Merge(Guid id, [FromBody] MergePatientsRequest request)
        {
            if (id != request.TargetPatientId) return BadRequest();
            try
            {
                var res = await _service.MergePatientsAsync(request);
                return Ok(HMS.API.Application.Common.ApiResponse<MergePatientsResult>.ForSuccess(res, 200));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(HMS.API.Application.Common.ApiResponse<object>.ForError("INVALID_OPERATION", ex.Message, 400));
            }
        }
    }
}