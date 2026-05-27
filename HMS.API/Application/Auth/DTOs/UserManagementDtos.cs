using System.ComponentModel.DataAnnotations;
using HMS.API.Application.Common;

namespace HMS.API.Application.Auth.DTOs;

public class UserListItemDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public Guid? TenantId { get; set; }
    public string? TenantName { get; set; }
    public bool IsLocked { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? FullName { get; set; }
    public string? PhotoUrl { get; set; }
    public DateTimeOffset? LastLogin { get; set; }
    public IReadOnlyCollection<Guid> RoleIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
}

public class UserDetailsDto : UserListItemDto
{
    public string? OtherNames { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public IReadOnlyCollection<AuthAuditDto> Activity { get; set; } = Array.Empty<AuthAuditDto>();
}

public class AuthAuditDto
{
    public DateTimeOffset PerformedAt { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
}

public class CreateUserRequest
{
    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? OtherNames { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public Guid? TenantId { get; set; }
    public IReadOnlyCollection<Guid> RoleIds { get; set; } = Array.Empty<Guid>();
}

public class UpdateUserRequest
{
    [EmailAddress]
    [MaxLength(320)]
    public string? Email { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? OtherNames { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public bool? IsLocked { get; set; }
    public IReadOnlyCollection<Guid>? RoleIds { get; set; }
}

public class PasswordRecoveryRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class PasswordRecoveryResetRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
}

public class PasswordRecoveryTokenStatusDto
{
    public bool Valid { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
}
