using System.ComponentModel.DataAnnotations;

namespace HMS.UI.Models.Auth;

public class ForgotPasswordViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
