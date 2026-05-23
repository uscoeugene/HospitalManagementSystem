namespace HMS.API.Application.Auth.DTOs
{
    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        // optional profile fields
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}