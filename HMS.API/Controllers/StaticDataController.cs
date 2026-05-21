using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using HMS.API.Application.Common;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class StaticDataController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public StaticDataController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpGet("medical")]
        public IActionResult Medical()
        {
            var path = Path.Combine(_env.WebRootPath ?? string.Empty, "data", "medical.json");
            if (!System.IO.File.Exists(path)) return NotFound(ApiResponse<object>.ForError("NOT_FOUND", "medical.json not found", 404));
            try
            {
                var txt = System.IO.File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<MedicalData>(txt, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return Ok(ApiResponse<MedicalData>.ForSuccess(data, 200));
            }
            catch
            {
                return StatusCode(500, ApiResponse<object>.ForError("INVALID_DATA", "Failed to parse medical.json", 500));
            }
        }

        [HttpGet("countries")]
        public IActionResult Countries()
        {
            var path = Path.Combine(_env.WebRootPath ?? string.Empty, "data", "countries.json");
            if (!System.IO.File.Exists(path)) return NotFound(ApiResponse<object>.ForError("NOT_FOUND", "countries.json not found", 404));
            try
            {
                var txt = System.IO.File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<CountryEntry[]>(txt, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return Ok(ApiResponse<CountryEntry[]>.ForSuccess(data, 200));
            }
            catch
            {
                return StatusCode(500, ApiResponse<object>.ForError("INVALID_DATA", "Failed to parse countries.json", 500));
            }
        }

        private record MedicalData(string[]? BloodGroups, string[]? Genotypes);
        private record CountryEntry(string Name, string[]? States);
    }
}
