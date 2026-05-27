using System.Threading.Tasks;
using HMS.API.Application.Auth.DTOs;

namespace HMS.API.Application.Auth
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request);
        Task<LoginResponse> RegisterAsync(RegisterRequest request);
        Task<RefreshResponse> RefreshAsync(RefreshRequest request);
        Task RevokeRefreshAsync(string refreshToken);
        Task RequestPasswordRecoveryAsync(string email, string resetBaseUrl);
        Task<PasswordRecoveryTokenStatusDto> ValidatePasswordResetTokenAsync(string token);
        Task ResetPasswordWithTokenAsync(string token, string newPassword);
    }
}
