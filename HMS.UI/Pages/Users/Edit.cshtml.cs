using System;
using System.Threading.Tasks;
using HMS.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HMS.UI.Pages.Users
{
    public class EditModel : PageModel
    {
        private readonly ApiClient _api;

        public EditModel(ApiClient api)
        {
            _api = api;
        }

        [BindProperty(SupportsGet = true)]
        public Guid Id { get; set; }

        [BindProperty]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            var u = await _api.GetAsync<LocalUserDto>($"/localusers/{Id}");
            if (u == null) RedirectToPage("/Users/Index");
            Id = u.Id;
            Username = u.Username;
            Email = u.Email ?? string.Empty;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var req = new { Username = Username, Password = Password, Email = Email };
            var res = await _api.PostAsync<object>($"/localusers/{Id}", req);
            return RedirectToPage("/Users/Index");
        }
    }
}
