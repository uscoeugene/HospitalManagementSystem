using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Application.Auth;
using HMS.API.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HMS.API.Application.Auth.DTOs;
using Microsoft.Extensions.Configuration;
using System;
using Microsoft.AspNetCore.Http;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LocalAuthController : ControllerBase
    {
        private readonly LocalAuthService _localAuth;
        private readonly LocalTokenService _tokenService;
        private readonly HmsDbContext _db;
        private readonly IConfiguration _config;

        public LocalAuthController(LocalAuthService localAuth, LocalTokenService tokenService, HmsDbContext db, IConfiguration config)
        {
            _localAuth = localAuth;
            _tokenService = tokenService;
            _db = db;
            _config = config;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            try
            {
                var resp = await _localAuth.LoginLocalAsync(req);

                // load roles and permissions from local caches
                var roles = await _db.Set<HMS.API.Domain.Auth.LocalRole>().AsNoTracking().Where(r => r.TenantId == resp.TenantId).Select(r => r.Name).ToArrayAsync();
                var perms = await _db.Set<HMS.API.Domain.Auth.LocalPermission>().AsNoTracking().Where(p => p.TenantId == resp.TenantId).Select(p => p.Code).ToArrayAsync();

                var token = _tokenService.BuildLocalJwt(resp.UserId, req.Username, resp.TenantId, roles, perms);

                return Ok(new { accessToken = token.token, expiresAt = token.expiresAt, userId = resp.UserId, tenantId = resp.TenantId, roles = roles });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
        }

        [HttpPost("set-cookie")]
        public IActionResult SetCookie([FromBody] SetCookieRequest req)
        {
            // set JWT as secure HttpOnly cookie
            var cookieOptions = new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Expires = DateTimeOffset.UtcNow.AddHours(8).UtcDateTime };
            Response.Cookies.Append("HmsAuth", req.Token, cookieOptions);
            return Ok();
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            try
            {
                await _localAuth.RegisterLocalAsync(req);
                return Created("/localauth/register", null);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    public class SetCookieRequest { public string Token { get; set; } = string.Empty; public bool Local { get; set; } }
}
