using System;
using System.Collections.Generic;

namespace HMS.UI.Models
{
    public class SystemMaintenanceViewModel
    {
        public List<TenantItem> Tenants { get; set; } = new();
        public List<MaintenanceScopeOptionViewModel> Scopes { get; set; } = new();
        public string SelectedScope { get; set; } = string.Empty;
        public Guid? SelectedTenantId { get; set; }
        public string? Confirmation { get; set; }
        public string? TenantCodeConfirmation { get; set; }
    }

    public class MaintenanceScopeOptionViewModel
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool RequiresTenant { get; set; }
        public bool RequiresTenantCodeConfirmation { get; set; }
    }
}
