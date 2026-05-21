using System;
using Microsoft.AspNetCore.Mvc;
using HMS.API.Application.Patient;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class VisitTypesController : ControllerBase
    {
        [HttpGet]
        public IActionResult List()
        {
            var values = Enum.GetNames(typeof(VisitType));
            return Ok(HMS.API.Application.Common.ApiResponse<string[]>.ForSuccess(values, 200));
        }
    }
}
