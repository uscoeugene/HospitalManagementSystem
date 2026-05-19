using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Application.Patient;
using HMS.API.Application.Patient.DTOs;
using HMS.API.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("debug")]
    public class DebugController : ControllerBase
    {
        private readonly IPatientService _patients;
        private readonly IHostEnvironment _env;

        public DebugController(IPatientService patients, IHostEnvironment env)
        {
            _patients = patients;
            _env = env;
        }

        [HttpGet("whoami")]
        public IActionResult WhoAmI()
        {
            var user = HttpContext.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return Ok(new { authenticated = false });
            }

            var claims = user.Claims.Select(c => new { c.Type, c.Value }).ToArray();
            return Ok(ApiResponse<object>.ForSuccess(new { authenticated = true, name = user.Identity?.Name, claims }, 200));
        }

        [HttpGet("patients")]
        public async Task<IActionResult> Patients([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var res = await _patients.ListPatientsAsync(q, page, pageSize);
                return Ok(ApiResponse<PagedResult<PatientResponse>>.ForSuccess(res, 200));
            }
            catch (Exception ex)
            {
                var msg = _env.IsDevelopment() ? ex.ToString() : ex.Message;
                return StatusCode(500, ApiResponse<object>.ForError("SERVER_ERROR", msg, 500));
            }
        }
    }
}
