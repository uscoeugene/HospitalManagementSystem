using System;
using System.Threading.Tasks;
using HMS.API.Application.Profile.DTOs;
using HMS.API.Infrastructure.Persistence;
using HMS.API.Domain.Profile;
using Microsoft.EntityFrameworkCore;

namespace HMS.API.Application.Profile
{
    /// <summary>
    /// Application-service implementing business rules for profiles.
    /// Responsible for creating/updating profiles and mapping domain -> DTO.
    /// Uses HmsDbContext (integrated) as requested.
    /// </summary>
    public class ProfileService : IProfileService
    {
        private readonly HmsDbContext _db;

        public ProfileService(HmsDbContext db)
        {
            _db = db;
        }

        public async Task<UserProfileDto?> GetByUserIdAsync(Guid userId)
        {
            var e = await _db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
            return e == null ? null : Map(e);
        }

        public async Task<UserProfileDto?> GetByIdAsync(Guid id)
        {
            var e = await _db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            return e == null ? null : Map(e);
        }

        public async Task<UserProfileDto> CreateOrUpdateAsync(Guid userId, UpdateUserProfileRequest request, Guid? updatedBy = null)
        {
            var entity = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (entity == null)
            {
                entity = new UserProfile
                {
                    UserId = userId,
                    FirstName = request.FirstName ?? string.Empty,
                    LastName = request.LastName ?? string.Empty,
                    OtherNames = request.OtherNames ?? string.Empty,
                    Gender = request.Gender ?? string.Empty,
                    DateOfBirth = request.DateOfBirth,
                    PhoneNumber = request.PhoneNumber ?? string.Empty,
                    Email = request.Email ?? string.Empty,
                    Address = request.Address ?? string.Empty,
                    PhotoUrl = request.PhotoUrl ?? string.Empty,
                    StaffNumber = request.StaffNumber ?? string.Empty,
                    Department = request.Department ?? string.Empty,
                    JobTitle = request.JobTitle ?? string.Empty,
                    IsMedicalStaff = request.IsMedicalStaff ?? false
                };

                _db.UserProfiles.Add(entity);
            }
            else
            {
                ApplyUpdates(entity, request);
            }

            await _db.SaveChangesAsync();
            return Map(entity);
        }

        public async Task<UserProfileDto?> UpdateForUserAsync(Guid userId, UpdateUserProfileRequest request, Guid? updatedBy = null)
        {
            var entity = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (entity == null)
            {
                // Optionally create on update - here we create a new profile if one doesn't exist.
                entity = new UserProfile { UserId = userId };
                ApplyUpdates(entity, request);
                _db.UserProfiles.Add(entity);
            }
            else
            {
                ApplyUpdates(entity, request);
            }

            await _db.SaveChangesAsync();
            return Map(entity);
        }

        private static void ApplyUpdates(UserProfile entity, UpdateUserProfileRequest req)
        {
            // Only set fields that the client provided (null means no change)
            if (req.FirstName is not null) entity.FirstName = req.FirstName;
            if (req.LastName is not null) entity.LastName = req.LastName;
            if (req.OtherNames is not null) entity.OtherNames = req.OtherNames;
            if (req.Gender is not null) entity.Gender = req.Gender;
            if (req.DateOfBirth.HasValue) entity.DateOfBirth = req.DateOfBirth;

            if (req.PhoneNumber is not null) entity.PhoneNumber = req.PhoneNumber;
            if (req.Email is not null) entity.Email = req.Email;
            if (req.Address is not null) entity.Address = req.Address;
            if (req.PhotoUrl is not null) entity.PhotoUrl = req.PhotoUrl;

            if (req.StaffNumber is not null) entity.StaffNumber = req.StaffNumber;
            if (req.Department is not null) entity.Department = req.Department;
            if (req.JobTitle is not null) entity.JobTitle = req.JobTitle;
            if (req.IsMedicalStaff.HasValue) entity.IsMedicalStaff = req.IsMedicalStaff.Value;
        }

        private static UserProfileDto Map(UserProfile p) => new()
        {
            Id = p.Id,
            UserId = p.UserId,
            FirstName = p.FirstName,
            LastName = p.LastName,
            OtherNames = p.OtherNames,
            Gender = p.Gender,
            DateOfBirth = p.DateOfBirth,
            PhoneNumber = p.PhoneNumber,
            Email = p.Email,
            Address = p.Address,
            PhotoUrl = p.PhotoUrl,
            StaffNumber = p.StaffNumber,
            Department = p.Department,
            JobTitle = p.JobTitle,
            IsMedicalStaff = p.IsMedicalStaff,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }
}
