using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;

namespace HMS.UI.Pages.Error
{
    public class IndexModel : PageModel
    {
        public int StatusCode { get; set; }
        public string? Message { get; set; }

        public void OnGet(int code)
        {
            StatusCode = code;
            Message = code switch
            {
                401 => "Unauthorized - please sign in.",
                403 => "Forbidden - you do not have permission to access this resource.",
                404 => "Not found - the requested resource could not be found.",
                500 => "Server error - an unexpected error occurred.",
                _ => "An error occurred."
            };
        }
    }
}
