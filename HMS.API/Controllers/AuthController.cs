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
        private readonly LocalAuthService _localAuthService;
        private readonly HMS.API.Application.Auth.LocalTokenService _localTokenService;
        private readonly AuthDbContext _authDb;

        public AuthController(IAuthService authService, LocalAuthService localAuthService, HMS.API.Application.Auth.LocalTokenService localTokenService, AuthDbContext authDb)
        {
            _authService = authService;
            _localAuthService = localAuthService;
            _localTokenService = localTokenService;
            _authDb = authDb;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            // Determine tenant context: prefer body TenantId, fallback to header X-Tenant-Id or X-Tenant-Code
            Guid? headerTenant = null;
            var tid = Request.Headers["X-Tenant-Id"].ToString();
            if (Guid.TryParse(tid, out var g)) headerTenant = g;

            // Support tenant code header for on-premise nodes: X-Tenant-Code
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

            var effectiveTenant = request.TenantId ?? headerTenant;

            var previous = HMS.API.Application.Common.CurrentTenantAccessor.CurrentTenantId;
            // Set the current tenant context (may be null)
            HMS.API.Application.Common.CurrentTenantAccessor.CurrentTenantId = effectiveTenant;

            try
            {
                try
                {
                    var resp = await _authService.LoginAsync(request);
                    var cookieOptions = new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Expires = resp.ExpiresAt.UtcDateTime };
                    Response.Cookies.Append("HmsAuth", resp.AccessToken, cookieOptions);
                    return Ok(resp);
                }
                catch (UnauthorizedAccessException uex)
                {
                    // If tenant-scoped login failed and a tenant was specified, try central (TenantId == null)
                    if (effectiveTenant != null)
                    {
                        try
                        {
                            HMS.API.Application.Common.CurrentTenantAccessor.CurrentTenantId = null;
                            var centralResp = await _authService.LoginAsync(request);
                            var cookieOptions = new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Expires = centralResp.ExpiresAt.UtcDateTime };
                            Response.Cookies.Append("HmsAuth", centralResp.AccessToken, cookieOptions);
                            return Ok(centralResp);
                        }
                        catch
                        {
                            // central login failed, proceed to local fallback
                        }
                        finally
                        {
                            HMS.API.Application.Common.CurrentTenantAccessor.CurrentTenantId = previous;
                        }
                    }

                    // Attempt local fallback
                    try
                    {
                        var localResp = await _localAuthService.LoginLocalAsync(request);

                        var roles = await _authDb.Set<LocalRole>().AsNoTracking().Where(r => r.TenantId == localResp.TenantId).Select(r => r.Name).ToArrayAsync();
                        var perms = await _authDb.Set<LocalPermission>().AsNoTracking().Where(p => p.TenantId == localResp.TenantId).Select(p => p.Code).ToArrayAsync();

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
                        var pd = new ProblemDetails { Title = "Unauthorized", Detail = uex.Message, Status = StatusCodes.Status401Unauthorized };
                        return Unauthorized(pd);
                    }
                }
            }
            catch (Exception ex)
            {
                // If central auth throws unexpected exception try local fallback
                try
                {
                    var localResp = await _localAuthService.LoginLocalAsync(request);

                    var roles = await _authDb.Set<LocalRole>().AsNoTracking().Where(r => r.TenantId == localResp.TenantId).Select(r => r.Name).ToArrayAsync();
                    var perms = await _authDb.Set<LocalPermission>().AsNoTracking().Where(p => p.TenantId == localResp.TenantId).Select(p => p.Code).ToArrayAsync();

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
                catch (UnauthorizedAccessException uex2)
                {
                    var pd = new ProblemDetails { Title = "Unauthorized", Detail = uex2.Message, Status = StatusCodes.Status401Unauthorized };
                    return Unauthorized(pd);
                }
                catch (Exception ex2)
                {
                    var pd = new ProblemDetails { Title = "Authentication Failure", Detail = ex2.Message, Status = StatusCodes.Status500InternalServerError };
                    return StatusCode(500, pd);
                }
            }
            finally
            {
                HMS.API.Application.Common.CurrentTenantAccessor.CurrentTenantId = previous;
            }

            return StatusCode(500);
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