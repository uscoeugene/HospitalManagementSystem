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

        [HttpPost]
        [HasPermission("patients.manage")]
        public async Task<ActionResult<PatientResponse>> Register([FromBody] RegisterPatientRequest request)
        {
            try
            {
                var resp = await _service.RegisterPatientAsync(request);
                return CreatedAtAction(nameof(Get), new { id = resp.Id }, resp);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        [HasPermission("patients.view")]
        public async Task<ActionResult<PatientResponse>> Get(Guid id)
        {
            var p = await _service.GetPatientAsync(id);
            if (p == null) return NotFound();
            return Ok(p);
        }

        [HttpGet]
        [HasPermission("patients.view")]
        public async Task<ActionResult<PagedResult<PatientResponse>>> List([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var res = await _service.ListPatientsAsync(q, page, pageSize);
            return Ok(res);
        }

        [HttpPost("{id}/visits")]
        [HasPermission("patients.manage")]
        public async Task<ActionResult<VisitResponse>> AddVisit(Guid id, [FromBody] AddVisitRequest request)
        {
            try
            {
                var v = await _service.AddVisitAsync(id, request);
                return CreatedAtAction(nameof(Get), new { id = id }, v);
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        [HttpGet("possible-duplicates")]
        [HasPermission("patients.manage")]
        public async Task<ActionResult<DuplicateCandidateDto[]>> PossibleDuplicates([FromQuery] string q, [FromQuery] double threshold = 0.75, [FromQuery] int dobToleranceDays = 365, [FromQuery] int mrnPrefixLength = 4)
        {
            var res = await _service.FindPossibleDuplicatesAsync(q, threshold, dobToleranceDays, mrnPrefixLength);
            return Ok(res);
        }

        [HttpPost("{id}/merge")]
        [HasPermission("patients.manage")]
        public async Task<ActionResult<MergePatientsResult>> Merge(Guid id, [FromBody] MergePatientsRequest request)
        {
            if (id != request.TargetPatientId) return BadRequest();
            try
            {
                var res = await _service.MergePatientsAsync(request);
                return Ok(res);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}