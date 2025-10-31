using Atlas.Common.DTOs;
using UserService.Models;

namespace UserService.Services;

public interface IUserService
{
    Task<AuthResponse?> RegisterAsync(RegisterRequest request);
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<AuthResponse?> RefreshTokenAsync(string refreshToken);
    Task<User?> GetUserByIdAsync(Guid userId);
    Task<bool> RevokeRefreshTokenAsync(string refreshToken);


    Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<bool> ResetPasswordAsync(ResetPasswordRequest request);
    Task<bool> ValidateResetTokenAsync(string token);
}
