using System;

namespace HMS.UI.Pages.Users
{
    public class TenantDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
