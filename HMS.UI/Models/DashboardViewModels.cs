using System;

namespace HMS.UI.Models
{
    public class DashboardCardViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconClass { get; set; } = "bi bi-grid";
        public string? BadgeText { get; set; }
        public string BadgeClass { get; set; } = "badge-soft-primary";
        public string? LinkUrl { get; set; }
        public string LinkText { get; set; } = "Open";
        public string? CountText { get; set; }
        public string? Footnote { get; set; }
    }

    public class DashboardViewModel
    {
        public string DisplayName { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public string DeploymentMode { get; set; } = "Unknown";
        public bool IsDevelopment { get; set; }
        public string[] Roles { get; set; } = Array.Empty<string>();
        public DashboardCardViewModel[] RoleCards { get; set; } = Array.Empty<DashboardCardViewModel>();
        public DashboardCardViewModel[] QueueCards { get; set; } = Array.Empty<DashboardCardViewModel>();
        public DashboardCardViewModel[] QuickActions { get; set; } = Array.Empty<DashboardCardViewModel>();
    }

    public class QueueItemViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string? Summary { get; set; }
        public string? Status { get; set; }
        public string BadgeClass { get; set; } = "badge-soft-primary";
        public string? LinkUrl { get; set; }
        public string LinkText { get; set; } = "Open";
        public string? Meta { get; set; }
    }

    public class QueuePageViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string[] Roles { get; set; } = Array.Empty<string>();
        public string EmptyMessage { get; set; } = "No items are waiting right now.";
        public string? FilterLabel { get; set; }
        public string? FilterValue { get; set; }
        public string[] FilterOptions { get; set; } = Array.Empty<string>();
        public string? PrimaryActionUrl { get; set; }
        public string? PrimaryActionText { get; set; }
        public PagedResult<QueueItemViewModel> ItemsPage { get; set; } = new PagedResult<QueueItemViewModel>();
    }
}
