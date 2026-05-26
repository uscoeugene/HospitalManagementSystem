using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Application.Profile;
using Microsoft.EntityFrameworkCore;
using HMS.API.Application.Profile.DTOs;
using HMS.API.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
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
        private readonly IWebHostEnvironment _env;

        public ProfileController(IProfileService profiles, ICurrentUserService currentUser, IAuthorizationService authorization, IWebHostEnvironment env)
        {
            _profiles = profiles;
            _currentUser = currentUser;
            _authorization = authorization;
            _env = env;
        }

        // GET /api/profile/providers
        [HttpGet("providers")]
        public async Task<ActionResult> Providers()
        {
            var list = await _profiles.ListProvidersAsync();
            return Ok(list);
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

            // If caller is tenant-scoped, ensure the requested user belongs to same tenant
            var callerTenant = _currentUser.TenantId;
            if (callerTenant.HasValue)
            {
                var authDb = HttpContext.RequestServices.GetService(typeof(HMS.API.Infrastructure.Auth.AuthDbContext)) as HMS.API.Infrastructure.Auth.AuthDbContext;
                if (authDb != null)
                {
                    var targetUser = await authDb.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId);
                    if (targetUser == null) return NotFound();
                    if (targetUser.TenantId != callerTenant) return Forbid();
                }
            }

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

        // POST /api/profile/me/photo
        [HttpPost("me/photo")]
        public async Task<IActionResult> UploadMyPhoto()
        {
            var uid = _currentUser.UserId;
            if (uid == null) return Unauthorized();

            var file = Request.Form.Files.FirstOrDefault();
            if (file == null || file.Length == 0) return BadRequest(new { error = "File required" });

            // validate basic file content-type
            var allowed = new[] { "image/png", "image/jpeg", "image/jpg", "image/gif" };
            if (!allowed.Contains(file.ContentType?.ToLower())) return BadRequest(new { error = "Unsupported file type" });

            var uploads = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "profiles");
            if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

            var ext = Path.GetExtension(file.FileName);
            var filename = $"{uid.Value.ToString()}_{Guid.NewGuid().ToString("N")}{ext}";
            var path = Path.Combine(uploads, filename);

            using (var fs = System.IO.File.Create(path))
            {
                await file.CopyToAsync(fs);
            }

            // store relative url
            var rel = $"/uploads/profiles/{filename}";

            // update profile photo url
            var upd = new UpdateUserProfileRequest { PhotoUrl = rel };
            await _profiles.UpdateForUserAsync(uid.Value, upd, uid);

            return Ok(new { url = rel });
        }

        // POST /api/profile/{userId}/photo  (admin)
        [HttpPost("{userId:guid}/photo")]
        public async Task<IActionResult> UploadPhotoForUser(Guid userId)
        {
            // require manage permission
            var authResult = await _authorization.AuthorizeAsync(User, "PROFILE.MANAGE");
            if (!authResult.Succeeded) return Forbid();

            var file = Request.Form.Files.FirstOrDefault();
            if (file == null || file.Length == 0) return BadRequest(new { error = "File required" });

            var allowed = new[] { "image/png", "image/jpeg", "image/jpg", "image/gif" };
            if (!allowed.Contains(file.ContentType?.ToLower())) return BadRequest(new { error = "Unsupported file type" });

            var uploads = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "profiles");
            if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

            var ext = Path.GetExtension(file.FileName);
            var filename = $"{userId.ToString()}_{Guid.NewGuid().ToString("N")}{ext}";
            var path = Path.Combine(uploads, filename);

            using (var fs = System.IO.File.Create(path))
            {
                await file.CopyToAsync(fs);
            }

            var rel = $"/uploads/profiles/{filename}";

            var upd = new UpdateUserProfileRequest { PhotoUrl = rel };
            await _profiles.UpdateForUserAsync(userId, upd, _currentUser.UserId);

            return Ok(new { url = rel });
        }
    }
}
