using UserService.Models;

namespace UserService.Services;

public interface IJwtService
{
    string GenerateToken(User user);
    string GenerateRefreshToken();
    Guid? ValidateToken(string token);
}
