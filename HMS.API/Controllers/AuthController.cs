using System.Threading.Tasks;
using HMS.API.Application.Auth;
using HMS.API.Application.Auth.DTOs;
using Microsoft.AspNetCore.Mvc;
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using HMS.API.Infrastructure.Auth;
using HMS.API.Domain.Auth;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly AuthDbContext _authDb;

        public AuthController(IAuthService authService, AuthDbContext authDb)
        {
            _authService = authService;
            _authDb = authDb;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            // Determine tenant context: prefer middleware-resolved value (HttpContext.Items["TenantId"]) then fall back to headers or request body for backward compatibility.
            Guid? resolvedByMiddleware = null;
            if (HttpContext.Items.TryGetValue("TenantId", out var val) && val is Guid gval)
            {
                resolvedByMiddleware = gval;
            }

            Guid? headerTenant = null;
            var tidHeader = Request.Headers["X-Tenant-Id"].ToString();
            if (Guid.TryParse(tidHeader, out var g)) headerTenant = g;

            var tenantCode = Request.Headers["X-Tenant-Code"].ToString();
            if (!headerTenant.HasValue && !string.IsNullOrWhiteSpace(tenantCode))
            {
                try
                {
                    var t = await _authDb.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Code == tenantCode);
                    if (t != null) headerTenant = t.Id;
                }
                catch { }
            }

            var previous = HMS.API.Application.Common.CurrentTenantAccessor.CurrentTenantId;
            // If middleware resolved a tenant, use that. Otherwise fall back to deprecated body/header values.
            var effectiveTenant = resolvedByMiddleware ?? request.TenantId ?? headerTenant;
            if (request.TenantId.HasValue && resolvedByMiddleware.HasValue)
            {
                // log warning - client-supplied tenantId is ignored
                try { Console.WriteLine("Warning: client-supplied tenantId will be ignored because middleware resolved tenant."); } catch { }
            }

            HMS.API.Application.Common.CurrentTenantAccessor.CurrentTenantId = effectiveTenant;

            try
            {
                var resp = await _authService.LoginAsync(request);
                var cookieOptions = new CookieOptions { HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Strict, Expires = resp.ExpiresAt.UtcDateTime };
                Response.Cookies.Append("HmsAuth", resp.AccessToken, cookieOptions);

                // Also set tenant cookies so UI can read tenant name without extra roundtrip. Do not rely on UI to parse response body.
                try
                {
                    if (resp.Tenant != null)
                    {
                        var tnOptions = new CookieOptions { HttpOnly = false, Secure = Request.IsHttps, SameSite = SameSiteMode.Strict };
                        Response.Cookies.Append("HmsTenantId", resp.Tenant.Id.ToString(), tnOptions);
                        Response.Cookies.Append("HmsTenantName", resp.Tenant.Name ?? string.Empty, tnOptions);
                    }
                    else if (resp.TenantId.HasValue)
                    {
                        var tnOptions = new CookieOptions { HttpOnly = false, Secure = Request.IsHttps, SameSite = SameSiteMode.Strict };
                        Response.Cookies.Append("HmsTenantId", resp.TenantId.Value.ToString(), tnOptions);
                    }
                }
                catch { }
                return Ok(resp);
            }
            catch (UnauthorizedAccessException uex)
            {
                var pd = new ProblemDetails { Title = "Unauthorized", Detail = uex.Message, Status = StatusCodes.Status401Unauthorized };
                return Unauthorized(pd);
            }
            catch (Exception ex)
            {
                var pd = new ProblemDetails { Title = "Authentication Failure", Detail = ex.Message, Status = StatusCodes.Status500InternalServerError };
                return StatusCode(500, pd);
            }
            finally
            {
                HMS.API.Application.Common.CurrentTenantAccessor.CurrentTenantId = previous;
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
            return Redirect("/Account/Login");
        }
    }
}