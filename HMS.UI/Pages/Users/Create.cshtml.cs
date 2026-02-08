using System.Threading.Tasks;
using HMS.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HMS.UI.Pages.Users
{
    public class CreateModel : PageModel
    {
        private readonly ApiClient _api;

        public CreateModel(ApiClient api)
        {
            _api = api;
        }

        [BindProperty]
        public string Username { get; set; } = string.Empty;
        [BindProperty]
        public string Password { get; set; } = string.Empty;
        [BindProperty]
        public string Email { get; set; } = string.Empty;

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            var req = new { Username = Username, Password = Password, Email = Email };
            var res = await _api.PostAsync<object>("/localusers", req);
            if (res == null) return BadRequest();
            return RedirectToPage("/Users/Index");
        }
    }
}
