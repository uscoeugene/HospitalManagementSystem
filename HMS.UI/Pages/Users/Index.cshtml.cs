using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HMS.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HMS.UI.Pages.Users
{
    public class IndexModel : PageModel
    {
        private readonly ApiClient _api;

        public IndexModel(ApiClient api)
        {
            _api = api;
        }

        public IEnumerable<LocalUserDto> Users { get; set; } = Array.Empty<LocalUserDto>();
        public Dictionary<Guid, string> TenantNames { get; set; } = new Dictionary<Guid, string>();

        [BindProperty(SupportsGet = true)]
        public int Page { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 20;

        [BindProperty(SupportsGet = true)]
        public string Search { get; set; } = string.Empty;

        public int Total { get; set; }

        public async Task OnGetAsync()
        {
            // Load tenants for mapping
            var tenants = await _api.GetAsync<IEnumerable<TenantDto>>("/tenants");
            if (tenants != null)
            {
                TenantNames = tenants.ToDictionary(t => t.Id, t => t.Name);
            }

            // call server-side paging endpoint
            var q = $"/users?page={Page}&pageSize={PageSize}" + (string.IsNullOrWhiteSpace(Search) ? string.Empty : $"&search={System.Net.WebUtility.UrlEncode(Search)}");
            var pg = await _api.GetAsync<PagedResult> (q);
            if (pg == null) return;
            Total = pg.Total;
            Users = pg.Items.Select(i => new LocalUserDto { Id = i.Id, Username = i.Username, Email = i.Email, TenantId = i.TenantId, IsLocked = i.IsLocked });
        }

        public async Task<IActionResult> OnPostLockAsync(Guid id)
        {
            var r = await _api.PostAsync<object>($"/users/{id}/lock", new { });
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUnlockAsync(Guid id)
        {
            var r = await _api.PostAsync<object>($"/users/{id}/unlock", new { });
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            var r = await _api.PostAsync<object>($"/users/{id}?_method=delete", new { });
            return RedirectToPage();
        }

        private class PagedResult
        {
            public int Total { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
            public Item[] Items { get; set; } = Array.Empty<Item>();

            public class Item
            {
                public Guid Id { get; set; }
                public string Username { get; set; } = string.Empty;
                public string? Email { get; set; }
                public Guid? TenantId { get; set; }
                public bool IsLocked { get; set; }
            }
        }
    }
}
