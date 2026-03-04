using System.Threading.Tasks;
using HMS.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;

namespace HMS.UI.Pages.LocalAuth
{
    public class LoginModel : PageModel
    {
        private readonly ApiClient _api;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(ApiClient api, ILogger<LoginModel> logger)
        {
            _api = api;
            _logger = logger;
        }

        [BindProperty]
        public string Username { get; set; } = string.Empty;
        [BindProperty]
        public string Password { get; set; } = string.Empty;

        public bool IsOnline { get; set; } = true;
        public string? Error { get; set; }

        public void OnGet()
        {
            IsOnline = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            IsOnline = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();

            try
            {
                var res = await _api.PostAsync<object>("/auth/login", new { Username = Username, Password = Password });
                if (res != null)
                {
                    // server sets cookie when login succeeds; just redirect
                    return RedirectToPage("/Index");
                }

                Error = "Invalid credentials or login failed.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Login request failed; attempting offline or reporting error");

                // If server is unreachable, try offline login by calling same endpoint; server will attempt local fallback
                try
                {
                    var offline = await _api.PostAsync<object>("/auth/login", new { Username = Username, Password = Password });
                    if (offline != null)
                    {
                        return RedirectToPage("/Index");
                    }
                }
                catch (Exception ex2)
                {
                    _logger.LogError(ex2, "Offline login attempt failed");
                }

                Error = "Login failed (network or server error).";
            }

            return Page();
        }
    }
}
