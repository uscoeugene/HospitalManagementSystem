using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Application.Auth;
using HMS.API.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HMS.API.Application.Auth.DTOs;
using Microsoft.Extensions.Configuration;

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
                var perms = new string[0];

                var token = _tokenService.BuildLocalJwt(resp.UserId, req.Username, resp.TenantId, roles, perms);

                return Ok(new { accessToken = token.token, expiresAt = token.expiresAt, userId = resp.UserId, tenantId = resp.TenantId, roles = roles });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
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
}
