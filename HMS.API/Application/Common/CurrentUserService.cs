using System;
using System.Linq;
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

        public Guid? TenantId
        {
            get
            {
                var t = _httpContextAccessor.HttpContext?.User?.FindFirst("tenant_id")?.Value;
                if (Guid.TryParse(t, out var id)) return id;
                return null;
            }
        }

        public System.Collections.Generic.IEnumerable<Guid> DepartmentIds
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;
                if (user == null) return System.Array.Empty<Guid>();
                // Prefer JSON array claim `departments` if present
                var deptClaim = user.Claims.FirstOrDefault(c => string.Equals(c.Type, "departments", System.StringComparison.OrdinalIgnoreCase));
                if (deptClaim != null && !string.IsNullOrWhiteSpace(deptClaim.Value))
                {
                    try
                    {
                        var arr = System.Text.Json.JsonSerializer.Deserialize<Guid[]>(deptClaim.Value);
                        if (arr != null) return arr.Distinct();
                    }
                    catch (Exception ex) { try { System.Diagnostics.Trace.TraceError(ex.ToString()); } catch { } }
                }

                var ids = user.Claims.Where(c => string.Equals(c.Type, "department_id", System.StringComparison.OrdinalIgnoreCase)).Select(c => { if (Guid.TryParse(c.Value, out var g)) return g; return Guid.Empty; }).Where(g => g != Guid.Empty).Distinct();
                return ids;
            }
        }

        public bool HasPermission(string permission)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return false;
            var perms = user.Claims.Where(c => c.Type == "permission").Select(c => c.Value);
            return perms.Contains(permission);
        }
    }
}