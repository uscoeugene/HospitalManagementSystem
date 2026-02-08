using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HMS.UI.Services;
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

        public IEnumerable<object> Users { get; set; } = Array.Empty<object>();

        public async Task OnGetAsync()
        {
            Users = await _api.GetAsync<IEnumerable<object>>("/localusers");
        }
    }
}
