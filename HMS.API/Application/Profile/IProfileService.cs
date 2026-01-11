using System;
using System.Threading.Tasks;
using HMS.API.Application.Profile.DTOs;

namespace HMS.API.Application.Profile
{
    public interface IProfileService
    {
        Task<UserProfileDto?> GetByUserIdAsync(Guid userId);
        Task<UserProfileDto?> GetByIdAsync(Guid id);
        Task<UserProfileDto> CreateOrUpdateAsync(Guid userId, UpdateUserProfileRequest request, Guid? updatedBy = null);
        Task<UserProfileDto?> UpdateForUserAsync(Guid userId, UpdateUserProfileRequest request, Guid? updatedBy = null);
    }
}
