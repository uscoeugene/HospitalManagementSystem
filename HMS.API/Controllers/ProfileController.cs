using System;
using System.Threading.Tasks;
using HMS.API.Application.Profile;
using HMS.API.Application.Profile.DTOs;
using HMS.API.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profiles;
        private readonly ICurrentUserService _currentUser;
        private readonly IAuthorizationService _authorization;

        public ProfileController(IProfileService profiles, ICurrentUserService currentUser, IAuthorizationService authorization)
        {
            _profiles = profiles;
            _currentUser = currentUser;
            _authorization = authorization;
        }

        // GET /api/profile/me
        [HttpGet("me")]
        public async Task<ActionResult<UserProfileDto>> GetMyProfile()
        {
            var uid = _currentUser.UserId;
            if (uid == null) return Unauthorized();

            var profile = await _profiles.GetByUserIdAsync(uid.Value);
            if (profile == null) return NotFound();
            return Ok(profile);
        }

        // PUT /api/profile/me
        [HttpPut("me")]
        public async Task<ActionResult<UserProfileDto>> UpdateMyProfile([FromBody] UpdateUserProfileRequest req)
        {
            var uid = _currentUser.UserId;
            if (uid == null) return Unauthorized();

            // Owner may update own profile without special permission; additional checks can be added here.
            var updated = await _profiles.UpdateForUserAsync(uid.Value, req, uid);
            return Ok(updated);
        }

        // GET /api/profile/{userId}
        [HttpGet("{userId:guid}")]
        public async Task<ActionResult<UserProfileDto>> GetProfile(Guid userId)
        {
            var me = _currentUser.UserId;
            if (me.HasValue && me.Value == userId)
            {
                var own = await _profiles.GetByUserIdAsync(userId);
                if (own == null) return NotFound();
                return Ok(own);
            }

            // Non-owner: require PROFILE.READ permission
            var authResult = await _authorization.AuthorizeAsync(User, "PROFILE.READ");
            if (!authResult.Succeeded) return Forbid();

            var profile = await _profiles.GetByUserIdAsync(userId);
            if (profile == null) return NotFound();
            return Ok(profile);
        }

        // PUT /api/profile/{userId}
        [HttpPut("{userId:guid}")]
        public async Task<ActionResult<UserProfileDto>> UpdateProfile(Guid userId, [FromBody] UpdateUserProfileRequest req)
        {
            var me = _currentUser.UserId;
            if (me.HasValue && me.Value == userId)
            {
                // Owner update - allowed
                var updated = await _profiles.UpdateForUserAsync(userId, req, me);
                return Ok(updated);
            }

            // Non-owner updates require manage permission
            var authResult = await _authorization.AuthorizeAsync(User, "PROFILE.MANAGE");
            if (!authResult.Succeeded) return Forbid();

            var updatedOther = await _profiles.UpdateForUserAsync(userId, req, me);
            if (updatedOther == null) return NotFound();
            return Ok(updatedOther);
        }
    }
}
