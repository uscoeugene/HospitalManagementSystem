using System.ComponentModel.DataAnnotations;

namespace HMS.UI.Models.Auth;

public class ResetPasswordViewModel
{
    public string Token { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string? Username { get; set; }

    [Required]
    [MinLength(8)]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = string.Empty;
}
