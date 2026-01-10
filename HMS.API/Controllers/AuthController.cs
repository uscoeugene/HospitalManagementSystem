using System.Threading.Tasks;
using HMS.API.Application.Auth;
using HMS.API.Application.Auth.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            try
            {
                var resp = await _authService.LoginAsync(request);
                return Ok(resp);
            }
            catch (System.UnauthorizedAccessException)
            {
                return Unauthorized();
            }
        }

        [HttpPost("register")]
        public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var resp = await _authService.RegisterAsync(request);
                return Ok(resp);
            }
            catch (System.InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<RefreshResponse>> Refresh([FromBody] RefreshRequest request)
        {
            try
            {
                var resp = await _authService.RefreshAsync(request);
                return Ok(resp);
            }
            catch (System.InvalidOperationException)
            {
                return BadRequest(new { error = "Invalid refresh token" });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
        {
            await _authService.RevokeRefreshAsync(request.RefreshToken);
            return NoContent();
        }
    }
}