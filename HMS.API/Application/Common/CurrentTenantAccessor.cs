using System;

namespace HMS.API.Application.Common
{
    // Simple ambient accessor for EF query filters to read current tenant
    // Set by middleware per-request.
    public static class CurrentTenantAccessor
    {
        private static AsyncLocal<Guid?> _tenantId = new AsyncLocal<Guid?>();

        public static Guid? CurrentTenantId
        {
            get => _tenantId.Value;
            set => _tenantId.Value = value;
        }

        public static void Clear()
        {
            _tenantId.Value = null;
        }
    }
}