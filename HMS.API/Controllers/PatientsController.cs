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

        [HttpPost("{id}/visits")]
        [HasPermission("patients.manage")]
        public async Task<IActionResult> AddVisit(Guid id, [FromBody] AddVisitRequest request)
        {
            try
            {
                var v = await _service.AddVisitAsync(id, request);
                return CreatedAtAction(nameof(Get), new { id = id }, HMS.API.Application.Common.ApiResponse<VisitResponse>.ForSuccess(v, 201));
            }
            catch (InvalidOperationException)
            {
                return NotFound(HMS.API.Application.Common.ApiResponse<object>.ForError("PATIENT_NOT_FOUND", "Patient not found", 404));
            }
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