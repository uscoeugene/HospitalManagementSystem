using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Application.Lab;
using HMS.API.Application.Lab.DTOs;
using HMS.API.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LabController : ControllerBase
    {
        private readonly ILabService _lab;
        private readonly IWebHostEnvironment _env;

        public LabController(ILabService lab, IWebHostEnvironment env)
        {
            _lab = lab;
            _env = env;
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
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, stack = ex.ToString() });
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
        public async Task<ActionResult> ListRequests([FromQuery] Guid? patientId, [FromQuery] Guid? visitId, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var res = await _lab.ListRequestsAsync(patientId, visitId, status, page, pageSize);
            return Ok(res);
        }

        [HttpPut("requests/{requestId}/items/{itemId}/result")]
        [HasPermission("lab.process")]
        public async Task<ActionResult<LabRequestDto>> UpdateResult(Guid requestId, Guid itemId, [FromBody] UpdateLabResultRequest req)
        {
            try
            {
                var res = await _lab.UpdateResultAsync(requestId, itemId, req);
                return Ok(res);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("requests/{requestId}/items/{itemId}/result/attachment")]
        [HasPermission("lab.process")]
        [RequestSizeLimit(20_000_000)]
        public async Task<ActionResult<LabRequestDto>> UploadResultAttachment(Guid requestId, Guid itemId, IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest(new { error = "Result attachment is required" });

            var allowed = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".txt", ".csv" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext)) return BadRequest(new { error = "Unsupported result attachment type" });

            try
            {
                var uploads = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "lab-results", requestId.ToString("N"));
                if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

                var fileName = $"{itemId:N}_{Guid.NewGuid():N}{ext}";
                var path = Path.Combine(uploads, fileName);
                await using (var stream = System.IO.File.Create(path))
                {
                    await file.CopyToAsync(stream);
                }

                var rel = $"/uploads/lab-results/{requestId:N}/{fileName}";
                var res = await _lab.AttachResultFileAsync(requestId, itemId, rel);
                return Ok(res);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
