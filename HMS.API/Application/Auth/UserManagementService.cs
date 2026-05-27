using HMS.API.Application.Auth.DTOs;
using HMS.API.Application.Common;
using HMS.API.Infrastructure.Auth;
using HMS.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HMS.API.Application.Auth;

public class UserManagementService : IUserManagementService
{
    private readonly AuthDbContext _authDb;
    private readonly HmsDbContext _hmsDb;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _hasher;

    public UserManagementService(
        AuthDbContext authDb,
        HmsDbContext hmsDb,
        ICurrentUserService currentUser,
        IPasswordHasher hasher)
    {
        _authDb = authDb;
        _hmsDb = hmsDb;
        _currentUser = currentUser;
        _hasher = hasher;
    }

    public async Task<PagedResult<UserListItemDto>> ListAsync(int page, int pageSize, string? search)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = ScopedUsersQuery();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                u.Username.Contains(term) ||
                u.Email.Contains(term));
        }

        var total = await query.CountAsync();
        var users = await query
            .OrderBy(u => u.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                User = u,
                RoleIds = u.UserRoles.Select(ur => ur.RoleId).ToList(),
                RoleNames = u.UserRoles.Select(ur => ur.Role.Name).ToList()
            })
            .ToListAsync();

        var userIds = users.Select(x => x.User.Id).ToArray();
        var tenantIds = users.Where(x => x.User.TenantId.HasValue).Select(x => x.User.TenantId!.Value).Distinct().ToArray();

        var profiles = await _hmsDb.UserProfiles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => userIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId);

        var tenantNames = await _authDb.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => tenantIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name);

        var lastLogins = await _authDb.AuthAudits
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => userIds.Contains(a.UserId) && a.Action == "Login")
            .GroupBy(a => a.UserId)
            .Select(g => new { g.Key, LastLogin = g.Max(x => x.PerformedAt) })
            .ToDictionaryAsync(x => x.Key, x => (DateTimeOffset?)x.LastLogin);

        var items = users.Select(x => MapUserListItem(x.User, x.RoleIds, x.RoleNames, profiles, tenantNames, lastLogins)).ToList();

        return new PagedResult<UserListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<UserDetailsDto?> GetAsync(Guid userId)
    {
        var user = await ScopedUsersQuery()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .SingleOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return null;
        }

        var profile = await _hmsDb.UserProfiles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(p => p.UserId == userId);

        var tenantName = user.TenantId.HasValue
            ? await _authDb.Tenants.AsNoTracking().IgnoreQueryFilters().Where(t => t.Id == user.TenantId.Value).Select(t => t.Name).SingleOrDefaultAsync()
            : null;

        var activity = await _authDb.AuthAudits
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.PerformedAt)
            .Take(20)
            .Select(a => new AuthAuditDto
            {
                PerformedAt = a.PerformedAt,
                Action = a.Action,
                Details = a.Details
            })
            .ToListAsync();

        var lastLogin = activity.FirstOrDefault(a => a.Action == "Login")?.PerformedAt;
        var fullName = BuildFullName(profile?.FirstName, profile?.OtherNames, profile?.LastName);

        return new UserDetailsDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = profile?.Email ?? user.Email,
            TenantId = user.TenantId,
            TenantName = tenantName,
            IsLocked = user.IsLocked,
            FirstName = profile?.FirstName,
            LastName = profile?.LastName,
            OtherNames = profile?.OtherNames,
            PhoneNumber = profile?.PhoneNumber,
            Department = profile?.Department,
            JobTitle = profile?.JobTitle,
            FullName = fullName,
            PhotoUrl = profile?.PhotoUrl,
            LastLogin = lastLogin,
            RoleIds = user.UserRoles.Select(ur => ur.RoleId).ToArray(),
            Roles = user.UserRoles.Select(ur => ur.Role.Name).OrderBy(x => x).ToArray(),
            Activity = activity
        };
    }

    public async Task<IReadOnlyCollection<Guid>> GetRoleIdsAsync(Guid userId)
    {
        await EnsureUserIsManageableAsync(userId);

        return await _authDb.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToArrayAsync();
    }

    public async Task<UserDetailsDto> CreateAsync(CreateUserRequest request)
    {
        var tenantId = ResolveTargetTenantId(request.TenantId);

        var usernameExists = await _authDb.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Username == request.Username);

        if (usernameExists)
        {
            throw new InvalidOperationException("Username already exists.");
        }

        var emailExists = await _authDb.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == request.Email && u.TenantId == tenantId);

        if (emailExists)
        {
            throw new InvalidOperationException("Email already exists for this scope.");
        }

        var roleIds = (request.RoleIds ?? Array.Empty<Guid>()).Distinct().ToArray();
        if (roleIds.Length == 0)
        {
            throw new InvalidOperationException("At least one role must be assigned when creating a user.");
        }

        var roles = await ScopedRolesQuery().Where(r => roleIds.Contains(r.Id)).ToListAsync();
        if (roles.Count != roleIds.Length)
        {
            throw new InvalidOperationException("One or more selected roles do not exist.");
        }

        var defaultUserRole = await ScopedRolesQuery().SingleOrDefaultAsync(r => r.Name == "User");
        if (defaultUserRole != null && roles.All(r => r.Id != defaultUserRole.Id))
        {
            roles.Add(defaultUserRole);
        }

        var user = new Domain.Auth.User
        {
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            PasswordHash = _hasher.Hash(request.Password),
            TenantId = tenantId
        };

        _authDb.Users.Add(user);
        await _authDb.SaveChangesAsync();

        foreach (var role in roles)
        {
            _authDb.UserRoles.Add(new Domain.Auth.UserRole { UserId = user.Id, RoleId = role.Id });
        }

        _authDb.AuthAudits.Add(new Domain.Auth.AuthAudit
        {
            UserId = user.Id,
            Action = "UserCreated",
            Details = $"Created by {_currentUser.UserId}"
        });

        await _authDb.SaveChangesAsync();

        var profile = await _hmsDb.UserProfiles
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(p => p.UserId == user.Id);

        if (profile == null)
        {
            profile = new Domain.Profile.UserProfile
            {
                UserId = user.Id,
                FirstName = request.FirstName?.Trim() ?? string.Empty,
                LastName = request.LastName?.Trim() ?? string.Empty,
                OtherNames = request.OtherNames?.Trim() ?? string.Empty,
                PhoneNumber = request.PhoneNumber?.Trim() ?? string.Empty,
                Email = request.Email.Trim(),
                Department = request.Department?.Trim() ?? string.Empty,
                JobTitle = request.JobTitle?.Trim() ?? string.Empty,
                TenantId = tenantId
            };

            _hmsDb.UserProfiles.Add(profile);
        }

        await _hmsDb.SaveChangesAsync();

        return (await GetAsync(user.Id))!;
    }

    public async Task<UserDetailsDto?> UpdateAsync(Guid userId, UpdateUserRequest request)
    {
        var user = await ScopedUsersQuery().SingleOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var email = request.Email.Trim();
            var emailExists = await _authDb.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Id != userId && u.Email == email && u.TenantId == user.TenantId);

            if (emailExists)
            {
                throw new InvalidOperationException("Email already exists for this scope.");
            }

            user.Email = email;
        }

        if (request.IsLocked.HasValue)
        {
            user.IsLocked = request.IsLocked.Value;
            user.LockedUntil = request.IsLocked.Value ? DateTimeOffset.UtcNow.AddYears(10) : null;
        }

        if (request.RoleIds != null)
        {
            var distinctRoleIds = request.RoleIds.Distinct().ToArray();
            var roles = await ScopedRolesQuery().Where(r => distinctRoleIds.Contains(r.Id)).Select(r => r.Id).ToListAsync();
            if (roles.Count != distinctRoleIds.Length)
            {
                throw new InvalidOperationException("One or more selected roles do not exist.");
            }

            var existingRoles = await _authDb.UserRoles.Where(ur => ur.UserId == userId).ToListAsync();
            var toRemove = existingRoles.Where(ur => !distinctRoleIds.Contains(ur.RoleId)).ToList();
            var currentRoleIds = existingRoles.Select(ur => ur.RoleId).ToHashSet();
            var toAdd = distinctRoleIds.Where(roleId => !currentRoleIds.Contains(roleId)).ToArray();

            if (toRemove.Count > 0)
            {
                _authDb.UserRoles.RemoveRange(toRemove);
            }

            foreach (var roleId in toAdd)
            {
                _authDb.UserRoles.Add(new Domain.Auth.UserRole { UserId = userId, RoleId = roleId });
            }
        }

        await _authDb.SaveChangesAsync();

        var profile = await _hmsDb.UserProfiles.IgnoreQueryFilters().SingleOrDefaultAsync(p => p.UserId == userId);
        if (profile == null)
        {
            profile = new Domain.Profile.UserProfile
            {
                UserId = userId,
                TenantId = user.TenantId,
                Email = user.Email
            };
            _hmsDb.UserProfiles.Add(profile);
        }

        if (request.Email != null)
        {
            profile.Email = request.Email.Trim();
        }

        if (request.FirstName != null) profile.FirstName = request.FirstName.Trim();
        if (request.LastName != null) profile.LastName = request.LastName.Trim();
        if (request.OtherNames != null) profile.OtherNames = request.OtherNames.Trim();
        if (request.PhoneNumber != null) profile.PhoneNumber = request.PhoneNumber.Trim();
        if (request.Department != null) profile.Department = request.Department.Trim();
        if (request.JobTitle != null) profile.JobTitle = request.JobTitle.Trim();

        await _hmsDb.SaveChangesAsync();

        _authDb.AuthAudits.Add(new Domain.Auth.AuthAudit
        {
            UserId = userId,
            Action = "UserUpdated",
            Details = $"Updated by {_currentUser.UserId}"
        });
        await _authDb.SaveChangesAsync();

        return await GetAsync(userId);
    }

    public async Task<bool> DeleteAsync(Guid userId)
    {
        var user = await ScopedUsersQuery().SingleOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return false;
        }

        if (_currentUser.UserId == userId)
        {
            throw new InvalidOperationException("You cannot delete your own account.");
        }

        _authDb.Users.Remove(user);

        var profile = await _hmsDb.UserProfiles.IgnoreQueryFilters().SingleOrDefaultAsync(p => p.UserId == userId);
        if (profile != null)
        {
            _hmsDb.UserProfiles.Remove(profile);
        }

        _authDb.AuthAudits.Add(new Domain.Auth.AuthAudit
        {
            UserId = userId,
            Action = "UserDeleted",
            Details = $"Deleted by {_currentUser.UserId}"
        });

        await _hmsDb.SaveChangesAsync();
        await _authDb.SaveChangesAsync();
        return true;
    }

    public async Task AssignRoleAsync(Guid userId, Guid roleId)
    {
        await EnsureUserIsManageableAsync(userId);

        var roleExists = await ScopedRolesQuery().AnyAsync(r => r.Id == roleId);
        if (!roleExists)
        {
            throw new InvalidOperationException("Role not found.");
        }

        var exists = await _authDb.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
        if (!exists)
        {
            _authDb.UserRoles.Add(new Domain.Auth.UserRole { UserId = userId, RoleId = roleId });
            await _authDb.SaveChangesAsync();
        }
    }

    public async Task RemoveRoleAsync(Guid userId, Guid roleId)
    {
        await EnsureUserIsManageableAsync(userId);

        var assignment = await _authDb.UserRoles.SingleOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
        if (assignment == null)
        {
            return;
        }

        _authDb.UserRoles.Remove(assignment);
        await _authDb.SaveChangesAsync();
    }

    private IQueryable<Domain.Auth.User> ScopedUsersQuery()
    {
        var query = _authDb.Users.IgnoreQueryFilters().AsQueryable().Where(u => !u.IsDeleted);

        if (_currentUser.TenantId.HasValue)
        {
            var tenantId = _currentUser.TenantId.Value;
            query = query.Where(u => u.TenantId == tenantId);
        }

        return query;
    }

    private IQueryable<Domain.Auth.Role> ScopedRolesQuery()
    {
        var query = _authDb.Roles
            .IgnoreQueryFilters()
            .AsQueryable()
            .Where(r => !r.IsDeleted);

        if (_currentUser.TenantId.HasValue)
        {
            var tenantId = _currentUser.TenantId.Value;
            query = query.Where(r => r.TenantId == null || r.TenantId == tenantId);
        }

        return query;
    }

    private Guid? ResolveTargetTenantId(Guid? requestedTenantId)
    {
        if (_currentUser.TenantId.HasValue)
        {
            return _currentUser.TenantId.Value;
        }

        return requestedTenantId;
    }

    private async Task EnsureUserIsManageableAsync(Guid userId)
    {
        var user = await ScopedUsersQuery().AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found or outside your management scope.");
        }
    }

    private static UserListItemDto MapUserListItem(
        Domain.Auth.User user,
        IReadOnlyCollection<Guid> roleIds,
        IReadOnlyCollection<string> roleNames,
        IReadOnlyDictionary<Guid, Domain.Profile.UserProfile> profiles,
        IReadOnlyDictionary<Guid, string> tenantNames,
        IReadOnlyDictionary<Guid, DateTimeOffset?> lastLogins)
    {
        profiles.TryGetValue(user.Id, out var profile);
        var fullName = BuildFullName(profile?.FirstName, profile?.OtherNames, profile?.LastName);

        return new UserListItemDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = profile?.Email ?? user.Email,
            TenantId = user.TenantId,
            TenantName = user.TenantId.HasValue && tenantNames.TryGetValue(user.TenantId.Value, out var tenantName) ? tenantName : null,
            IsLocked = user.IsLocked,
            FirstName = profile?.FirstName,
            LastName = profile?.LastName,
            FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName,
            PhotoUrl = profile?.PhotoUrl,
            LastLogin = lastLogins.TryGetValue(user.Id, out var lastLogin) ? lastLogin : null,
            RoleIds = roleIds.ToArray(),
            Roles = roleNames.OrderBy(x => x).ToArray()
        };
    }

    private static string BuildFullName(string? firstName, string? otherNames, string? lastName)
    {
        return string.Join(" ", new[] { firstName, otherNames, lastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
    }
}
