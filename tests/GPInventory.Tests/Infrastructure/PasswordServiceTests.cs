using FluentAssertions;
using GPInventory.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Moq;

namespace GPInventory.Tests.Infrastructure;

public class PasswordServiceTests
{
    private readonly PasswordService _passwordService;

    public PasswordServiceTests()
    {
        _passwordService = new PasswordService();
    }

    [Fact]
    public void HashPassword_ShouldReturnHashedPassword()
    {
        // Arrange
        var password = "password123";
        var salt = "testsalt";

        // Act
        var hashedPassword = _passwordService.HashPassword(password, salt);

        // Assert
        hashedPassword.Should().NotBeNullOrEmpty();
        hashedPassword.Should().NotBe(password);
        hashedPassword.Should().NotBe(salt);
    }

    [Fact]
    public void HashPassword_WithSameInputs_ShouldReturnSameHash()
    {
        // Arrange
        var password = "password123";
        var salt = "testsalt";

        // Act
        var hash1 = _passwordService.HashPassword(password, salt);
        var hash2 = _passwordService.HashPassword(password, salt);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void HashPassword_WithDifferentSalt_ShouldReturnDifferentHash()
    {
        // Arrange
        var password = "password123";
        var salt1 = "testsalt1";
        var salt2 = "testsalt2";

        // Act
        var hash1 = _passwordService.HashPassword(password, salt1);
        var hash2 = _passwordService.HashPassword(password, salt2);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        var password = "password123";
        var salt = "testsalt";
        var hashedPassword = _passwordService.HashPassword(password, salt);

        // Act
        var result = _passwordService.VerifyPassword(password, hashedPassword, salt);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ShouldReturnFalse()
    {
        // Arrange
        var password = "password123";
        var wrongPassword = "wrongpassword";
        var salt = "testsalt";
        var hashedPassword = _passwordService.HashPassword(password, salt);

        // Act
        var result = _passwordService.VerifyPassword(wrongPassword, hashedPassword, salt);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GenerateSalt_ShouldReturnNonEmptyString()
    {
        // Act
        var salt = _passwordService.GenerateSalt();

        // Assert
        salt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateSalt_ShouldReturnDifferentSalts()
    {
        // Act
        var salt1 = _passwordService.GenerateSalt();
        var salt2 = _passwordService.GenerateSalt();

        // Assert
        salt1.Should().NotBe(salt2);
    }
}
