using System;
using System.Threading.Tasks;
using HMS.API.Application.Lab;
using HMS.API.Application.Lab.DTOs;
using HMS.API.Security;
using Microsoft.AspNetCore.Mvc;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LabController : ControllerBase
    {
        private readonly ILabService _lab;

        public LabController(ILabService lab)
        {
            _lab = lab;
        }

        [HttpGet("tests")]
        [HasPermission("lab.view")]
        public async Task<ActionResult> ListTests()
        {
            var t = await _lab.ListTestsAsync();
            return Ok(t);
        }

        [HttpPost("tests")]
        [HasPermission("lab.manage")]
        public async Task<ActionResult<LabTestDto>> CreateTest([FromBody] LabTestDto dto)
        {
            var res = await _lab.CreateTestAsync(dto);
            return CreatedAtAction(nameof(ListTests), new { id = res.Id }, res);
        }

        [HttpPost("requests")]
        [HasPermission("lab.request")]
        public async Task<ActionResult<LabRequestDto>> CreateRequest([FromBody] CreateLabRequest req)
        {
            try
            {
                var r = await _lab.CreateRequestAsync(req);
                return CreatedAtAction(nameof(GetRequest), new { id = r.Id }, r);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("requests/{id}")]
        [HasPermission("lab.view")]
        public async Task<ActionResult<LabRequestDto>> GetRequest(Guid id)
        {
            var r = await _lab.GetRequestAsync(id);
            if (r == null) return NotFound();
            return Ok(r);
        }

        [HttpGet("requests")]
        [HasPermission("lab.view")]
        public async Task<ActionResult> ListRequests([FromQuery] Guid? patientId, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var res = await _lab.ListRequestsAsync(patientId, status, page, pageSize);
            return Ok(res);
        }
    }
}