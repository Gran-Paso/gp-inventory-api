using FluentAssertions;
using GPInventory.Application.DTOs.Auth;
using GPInventory.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Moq;

namespace GPInventory.Tests.Infrastructure;

public class TokenServiceTests
{
    private readonly TokenService _tokenService;
    private readonly Mock<IConfiguration> _configurationMock;

    public TokenServiceTests()
    {
        _configurationMock = new Mock<IConfiguration>();
        
        // Setup configuration mock
        var jwtSectionMock = new Mock<IConfigurationSection>();
        jwtSectionMock.Setup(x => x["SecretKey"]).Returns("this-is-a-super-secret-key-that-is-very-long-and-secure");
        jwtSectionMock.Setup(x => x["Issuer"]).Returns("GPInventory");
        jwtSectionMock.Setup(x => x["Audience"]).Returns("GPInventory");
        
        _configurationMock.Setup(x => x.GetSection("JwtSettings")).Returns(jwtSectionMock.Object);
        _configurationMock.Setup(x => x["JwtSettings:SecretKey"]).Returns("this-is-a-super-secret-key-that-is-very-long-and-secure");
        _configurationMock.Setup(x => x["JwtSettings:Issuer"]).Returns("GPInventory");
        _configurationMock.Setup(x => x["JwtSettings:Audience"]).Returns("GPInventory");
        
        _tokenService = new TokenService(_configurationMock.Object);
    }

    [Fact]
    public void GenerateToken_ShouldReturnValidToken()
    {
        // Arrange
        var userDto = new UserDto
        {
            Id = 1,
            Email = "test@example.com",
            Name = "John",
            LastName = "Doe",
            Active = true
        };

        // Act
        var token = _tokenService.GenerateToken(userDto);

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Split('.').Should().HaveCount(3); // JWT has 3 parts
    }

    [Fact]
    public void ValidateToken_WithValidToken_ShouldReturnTrue()
    {
        // Arrange
        var userDto = new UserDto
        {
            Id = 1,
            Email = "test@example.com",
            Name = "John",
            LastName = "Doe",
            Active = true
        };

        var token = _tokenService.GenerateToken(userDto);

        // Act
        var result = _tokenService.ValidateToken(token);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateToken_WithInvalidToken_ShouldReturnFalse()
    {
        // Arrange
        var invalidToken = "invalid.token.here";

        // Act
        var result = _tokenService.ValidateToken(invalidToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateToken_WithEmptyToken_ShouldReturnFalse()
    {
        // Arrange
        var emptyToken = "";

        // Act
        var result = _tokenService.ValidateToken(emptyToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetUserIdFromToken_WithValidToken_ShouldReturnUserId()
    {
        // Arrange
        var userDto = new UserDto
        {
            Id = 123,
            Email = "test@example.com",
            Name = "John",
            LastName = "Doe",
            Active = true
        };

        var token = _tokenService.GenerateToken(userDto);

        // Act
        var userId = _tokenService.GetUserIdFromToken(token);

        // Assert
        userId.Should().Be(123);
    }

    [Fact]
    public void GetUserIdFromToken_WithInvalidToken_ShouldReturnNull()
    {
        // Arrange
        var invalidToken = "invalid.token.here";

        // Act
        var userId = _tokenService.GetUserIdFromToken(invalidToken);

        // Assert
        userId.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithMissingSecretKey_ShouldThrowException()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(x => x["JwtSettings:SecretKey"]).Returns((string?)null);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new TokenService(configMock.Object));
    }
}
