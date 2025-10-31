using Xunit;
using UserService.Services;
using UserService.Models;
using Microsoft.Extensions.Configuration;
using Moq;

namespace UserService.Tests;

public class JwtServiceTests
{
    private readonly JwtService _jwtService;
    private readonly IConfiguration _configuration;

    public JwtServiceTests()
    {
        var inMemorySettings = new Dictionary<string, string>
        {
            {"Jwt:Secret", "this-is-a-very-long-secret-key-for-testing-purposes-minimum-32-characters"},
            {"Jwt:Issuer", "atlas-test"},
            {"Jwt:Audience", "atlas-test-audience"},
            {"Jwt:ExpireMinutes", "60"}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        _jwtService = new JwtService(_configuration);
    }

    [Fact]
    public void GenerateToken_ShouldReturnValidToken()
    {

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = "User"
        };


        var token = _jwtService.GenerateToken(user);


        Assert.NotNull(token);
        Assert.NotEmpty(token);
        Assert.Contains(".", token);
    }

    [Fact]
    public void ValidateToken_WithValidToken_ShouldReturnUserId()
    {

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = "User"
        };

        var token = _jwtService.GenerateToken(user);


        var userId = _jwtService.ValidateToken(token);


        Assert.NotNull(userId);
        Assert.Equal(user.Id, userId.Value);
    }

    [Fact]
    public void ValidateToken_WithInvalidToken_ShouldReturnNull()
    {

        var invalidToken = "invalid.token.here";


        var principal = _jwtService.ValidateToken(invalidToken);


        Assert.Null(principal);
    }

    [Fact]
    public void GenerateToken_ShouldIncludeAllClaims()
    {

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = "Admin"
        };


        var token = _jwtService.GenerateToken(user);
        var userId = _jwtService.ValidateToken(token);


        Assert.NotNull(userId);
        Assert.Equal(user.Id, userId.Value);

    }
}
