using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Common
{
    public class Tenant : BaseEntity
    {
        // Human-readable name for the hospital/tenant
        public string Name { get; set; } = string.Empty;

        // Short code used by offline instances to identify the tenant (e.g., HOSP123)
        public string Code { get; set; } = string.Empty;

        // Optional: public facing identifier for sync (can be same as Code)
        public string? ExternalId { get; set; }

        // Contact or metadata
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }

        // Mark whether this tenant record represents the central (online) authority
        public bool IsCentral { get; set; } = false;
    }
}