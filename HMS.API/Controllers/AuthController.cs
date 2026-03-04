using System.Threading.Tasks;
using HMS.API.Application.Auth;
using HMS.API.Application.Auth.DTOs;
using Microsoft.AspNetCore.Mvc;
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using HMS.API.Application.Common;
using HMS.API.Infrastructure.Persistence;
using HMS.API.Domain.Auth;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly LocalAuthService _localAuthService;
        private readonly HMS.API.Application.Auth.LocalTokenService _localTokenService;
        private readonly HmsDbContext _hmsDb;

        public AuthController(IAuthService authService, LocalAuthService localAuthService, HMS.API.Application.Auth.LocalTokenService localTokenService, HmsDbContext hmsDb)
        {
            _authService = authService;
            _localAuthService = localAuthService;
            _localTokenService = localTokenService;
            _hmsDb = hmsDb;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            // Try central login first
            try
            {
                var resp = await _authService.LoginAsync(request);

                // set cookie with central token
                var cookieOptions = new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Expires = resp.ExpiresAt.UtcDateTime };
                Response.Cookies.Append("HmsAuth", resp.AccessToken, cookieOptions);

                return Ok(resp);
            }
            catch (UnauthorizedAccessException)
            {
                // central auth rejected credentials - do not fallback to local
                return Unauthorized();
            }
            catch (Exception)
            {
                // central auth failed (likely unreachable) - attempt local auth fallback
                try
                {
                    var localResp = await _localAuthService.LoginLocalAsync(request);

                    // fetch roles and permissions from local cache
                    var roles = await _hmsDb.Set<LocalRole>().AsNoTracking().Where(r => r.TenantId == localResp.TenantId).Select(r => r.Name).ToArrayAsync();
                    var perms = await _hmsDb.Set<LocalPermission>().AsNoTracking().Where(p => p.TenantId == localResp.TenantId).Select(p => p.Code).ToArrayAsync();

                    var (tokenString, expiresAt) = _localTokenService.BuildLocalJwt(localResp.UserId, request.Username, localResp.TenantId, roles, perms);

                    var cookieOptions = new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Expires = expiresAt.UtcDateTime };
                    Response.Cookies.Append("HmsAuth", tokenString, cookieOptions);

                    var outResp = new LoginResponse
                    {
                        AccessToken = tokenString,
                        ExpiresAt = expiresAt,
                        UserId = localResp.UserId,
                        TenantId = localResp.TenantId,
                        Permissions = perms
                    };

                    return Ok(outResp);
                }
                catch (UnauthorizedAccessException)
                {
                    return Unauthorized();
                }
                catch (Exception)
                {
                    return StatusCode(500);
                }
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
            // clear cookie
            Response.Cookies.Delete("HmsAuth");
            return NoContent();
        }

        // Clear cookie and redirect to login - useful for UI logout without providing refresh token
        [HttpGet("logout-cookie")]
        public IActionResult LogoutCookie()
        {
            Response.Cookies.Delete("HmsAuth");
            return Redirect("/LocalAuth/Login");
        }
    }
}