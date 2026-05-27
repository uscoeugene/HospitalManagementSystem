using HMS.API.Application.Auth.DTOs;
using HMS.API.Application.Common;

namespace HMS.API.Application.Auth;

public interface IUserManagementService
{
    Task<PagedResult<UserListItemDto>> ListAsync(int page, int pageSize, string? search);
    Task<UserDetailsDto?> GetAsync(Guid userId);
    Task<IReadOnlyCollection<Guid>> GetRoleIdsAsync(Guid userId);
    Task<UserDetailsDto> CreateAsync(CreateUserRequest request);
    Task<UserDetailsDto?> UpdateAsync(Guid userId, UpdateUserRequest request);
    Task<bool> DeleteAsync(Guid userId);
    Task AssignRoleAsync(Guid userId, Guid roleId);
    Task RemoveRoleAsync(Guid userId, Guid roleId);
}
