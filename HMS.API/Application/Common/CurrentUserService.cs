using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace HMS.API.Application.Common
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid? UserId
        {
            get
            {
                var sub = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(sub, out var id)) return id;
                // fallback to JWT 'sub' claim
                sub = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;
                if (Guid.TryParse(sub, out id)) return id;
                sub = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;
                if (Guid.TryParse(sub, out id)) return id;
                return null;
            }
        }
    }
}