using HMS.API.Application.Auth;
using HMS.API.Application.Auth.DTOs;
using HMS.API.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HMS.API.Controllers;

[ApiController]
[Route("auth/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserManagementService _users;
    private readonly IPasswordHasher _hasher;
    private readonly HMS.API.Infrastructure.Auth.AuthDbContext _authDb;

    public UsersController(
        IUserManagementService users,
        IPasswordHasher hasher,
        HMS.API.Infrastructure.Auth.AuthDbContext authDb)
    {
        _users = users;
        _hasher = hasher;
        _authDb = authDb;
    }

    [HttpGet]
    [HasPermission("users.manage")]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
    {
        var result = await _users.ListAsync(page, pageSize, search);
        return Ok(result);
    }

    [HttpGet("available-roles")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> AvailableRoles([FromServices] HMS.API.Application.Common.ICurrentUserService currentUser)
    {
        var rolesQuery = _authDb.Roles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => !r.IsDeleted);

        if (currentUser.TenantId.HasValue)
        {
            var tenantId = currentUser.TenantId.Value;
            rolesQuery = rolesQuery.Where(r => r.TenantId == null || r.TenantId == tenantId);
        }

        var roles = await rolesQuery
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.Name)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Description,
                Permissions = r.RolePermissions.Select(rp => rp.Permission.Code)
            })
            .ToListAsync();

        return Ok(roles);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> Get(Guid id)
    {
        var user = await _users.GetAsync(id);
        return user == null ? NotFound() : Ok(user);
    }

    [HttpPost]
    [HasPermission("users.manage")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        try
        {
            var created = await _users.CreateAsync(request);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            var updated = await _users.UpdateAsync(id, request);
            return updated == null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var deleted = await _users.DeleteAsync(id);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/roles")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> GetUserRoles(Guid id)
    {
        try
        {
            var roles = await _users.GetRoleIdsAsync(id);
            return Ok(roles);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/roles/{roleId:guid}")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> AssignRole(Guid id, Guid roleId)
    {
        try
        {
            await _users.AssignRoleAsync(id, roleId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/roles/{roleId:guid}")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> RemoveRole(Guid id, Guid roleId)
    {
        try
        {
            await _users.RemoveRoleAsync(id, roleId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/reset-password")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest req)
    {
        var user = await _authDb.Users.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
        if (user == null) return NotFound(new { error = "User not found" });

        user.PasswordHash = !string.IsNullOrWhiteSpace(req.NewPasswordPlain)
            ? _hasher.Hash(req.NewPasswordPlain)
            : req.NewPasswordHash ?? string.Empty;

        user.IsLocked = false;
        user.LockedUntil = null;
        await _authDb.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/lock")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> LockUser(Guid id, [FromBody] LockRequest req)
    {
        var updated = await _users.UpdateAsync(id, new UpdateUserRequest { IsLocked = true });
        if (updated == null) return NotFound(new { error = "User not found" });

        if (req.LockedUntil.HasValue)
        {
            var user = await _authDb.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == id);
            user.LockedUntil = req.LockedUntil.Value;
            await _authDb.SaveChangesAsync();
        }

        return NoContent();
    }

    [HttpPost("{id:guid}/unlock")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> UnlockUser(Guid id)
    {
        var updated = await _users.UpdateAsync(id, new UpdateUserRequest { IsLocked = false });
        return updated == null ? NotFound(new { error = "User not found" }) : NoContent();
    }

    public class ResetPasswordRequest
    {
        public string? NewPasswordHash { get; set; }
        public string? NewPasswordPlain { get; set; }
    }

    public class LockRequest
    {
        public DateTimeOffset? LockedUntil { get; set; }
    }
}
