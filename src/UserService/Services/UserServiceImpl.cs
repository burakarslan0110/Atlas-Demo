using Atlas.Common.DTOs;
using Atlas.EventBus;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using UserService.Data;
using UserService.Models;
using BCrypt.Net;

namespace UserService.Services;

public class UserServiceImpl : IUserService
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IConnectionMultiplexer _redis;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<UserServiceImpl> _logger;
    private readonly IConfiguration _configuration;

    public UserServiceImpl(
        ApplicationDbContext context,
        IJwtService jwtService,
        IConnectionMultiplexer redis,
        IEventPublisher eventPublisher,
        IConfiguration configuration,
        ILogger<UserServiceImpl> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _redis = redis;
        _eventPublisher = eventPublisher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {

        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            _logger.LogWarning("Registration attempt with existing email: {Email}", request.Email);
            return null;
        }


        var user = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = Atlas.Common.Constants.AppConstants.Roles.User
        };

        _context.Users.Add(user);


        var jwtToken = _jwtService.GenerateToken(user);
        var refreshTokenString = _jwtService.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            Token = refreshTokenString,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            User = user
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();


        var db = _redis.GetDatabase();
        await db.StringSetAsync($"refresh_token:{refreshTokenString}", user.Id.ToString(), TimeSpan.FromDays(7));

        _logger.LogInformation("User registered successfully: {Email}", user.Email);

        return new AuthResponse
        {
            Token = jwtToken,
            RefreshToken = refreshTokenString,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            User = MapToUserDto(user)
        };
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for email: {Email}", request.Email);
            return null;
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login attempt for inactive user: {Email}", request.Email);
            return null;
        }


        var jwtToken = _jwtService.GenerateToken(user);
        var refreshTokenString = _jwtService.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            Token = refreshTokenString,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            User = user
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();


        var db = _redis.GetDatabase();
        await db.StringSetAsync($"refresh_token:{refreshTokenString}", user.Id.ToString(), TimeSpan.FromDays(7));

        _logger.LogInformation("User logged in successfully: {Email}", user.Email);

        return new AuthResponse
        {
            Token = jwtToken,
            RefreshToken = refreshTokenString,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            User = MapToUserDto(user)
        };
    }

    public async Task<AuthResponse?> RefreshTokenAsync(string refreshToken)
    {
        var db = _redis.GetDatabase();
        var userIdString = await db.StringGetAsync($"refresh_token:{refreshToken}");

        if (userIdString.IsNullOrEmpty || !Guid.TryParse(userIdString, out var userId))
        {
            _logger.LogWarning("Invalid refresh token attempt");
            return null;
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null || !user.IsActive)
        {
            return null;
        }


        var jwtToken = _jwtService.GenerateToken(user);
        var newRefreshTokenString = _jwtService.GenerateRefreshToken();

        var newRefreshToken = new RefreshToken
        {
            Token = newRefreshTokenString,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            User = user
        };


        var oldToken = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == refreshToken);
        if (oldToken != null)
        {
            oldToken.IsRevoked = true;
        }

        _context.RefreshTokens.Add(newRefreshToken);
        await _context.SaveChangesAsync();


        await db.KeyDeleteAsync($"refresh_token:{refreshToken}");
        await db.StringSetAsync($"refresh_token:{newRefreshTokenString}", user.Id.ToString(), TimeSpan.FromDays(7));

        _logger.LogInformation("Token refreshed for user: {Email}", user.Email);

        return new AuthResponse
        {
            Token = jwtToken,
            RefreshToken = newRefreshTokenString,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            User = MapToUserDto(user)
        };
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _context.Users.FindAsync(userId);
    }

    public async Task<bool> RevokeRefreshTokenAsync(string refreshToken)
    {
        var token = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == refreshToken);
        if (token == null)
        {
            return false;
        }

        token.IsRevoked = true;
        await _context.SaveChangesAsync();


        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync($"refresh_token:{refreshToken}");

        return true;
    }

    public async Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null)
        {

            _logger.LogWarning("Password reset requested for non-existent email: {Email}", request.Email);
            return true;
        }


        var resetToken = Guid.NewGuid().ToString();
        user.PasswordResetToken = resetToken;
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);

        await _context.SaveChangesAsync();


        var frontendUrl = _configuration["FrontendUrl"] ?? "http://localhost:5004";
        var resetUrl = $"{frontendUrl}/Account/ResetPassword?token={resetToken}";


        var passwordResetEvent = new
        {
            UserId = user.Id.ToString(),
            Email = user.Email,
            UserName = $"{user.FirstName} {user.LastName}",
            ResetToken = resetToken,
            ResetUrl = resetUrl
        };

        await _eventPublisher.PublishAsync("user.events", "password.reset.requested", passwordResetEvent);

        _logger.LogInformation("Password reset requested for user: {Email}", user.Email);
        return true;
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.PasswordResetToken == request.Token &&
            u.PasswordResetTokenExpiry > DateTime.UtcNow);

        if (user == null)
        {
            _logger.LogWarning("Invalid or expired reset token: {Token}", request.Token);
            return false;
        }


        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Password reset successful for user: {Email}", user.Email);
        return true;
    }

    public async Task<bool> ValidateResetTokenAsync(string token)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.PasswordResetToken == token &&
            u.PasswordResetTokenExpiry > DateTime.UtcNow);

        return user != null;
    }

    private UserDto MapToUserDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };
    }
}
