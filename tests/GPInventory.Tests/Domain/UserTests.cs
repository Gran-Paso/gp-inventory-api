using FluentAssertions;
using GPInventory.Domain.Entities;

namespace GPInventory.Tests.Domain;

public class UserTests
{
    [Fact]
    public void User_Constructor_ShouldCreateUserWithCorrectProperties()
    {
        // Arrange
        var email = "test@example.com";
        var name = "John";
        var lastName = "Doe";
        var password = "password123";
        var salt = "randomsalt";

        // Act
        var user = new User(email, name, lastName, password, salt);

        // Assert
        user.Mail.Should().Be(email);
        user.Name.Should().Be(name);
        user.LastName.Should().Be(lastName);
        user.Password.Should().Be(password);
        user.Salt.Should().Be(salt);
        user.Active.Should().BeTrue();
        user.UserBusinesses.Should().NotBeNull();
        user.UserBusinesses.Should().BeEmpty();
    }

    [Fact]
    public void User_VerifyPassword_ShouldReturnTrue_WhenPasswordIsCorrect()
    {
        // Arrange
        var user = new User();
        var password = "password123";
        user.Password = BCrypt.Net.BCrypt.HashPassword(password + user.Salt);

        // Act
        var result = user.VerifyPassword(password);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void User_VerifyPassword_ShouldReturnFalse_WhenPasswordIsIncorrect()
    {
        // Arrange
        var user = new User();
        var password = "password123";
        var wrongPassword = "wrongpassword";
        user.Password = BCrypt.Net.BCrypt.HashPassword(password + user.Salt);

        // Act
        var result = user.VerifyPassword(wrongPassword);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void User_UpdatePassword_ShouldUpdatePasswordAndSalt()
    {
        // Arrange
        var user = new User();
        var originalPassword = user.Password;
        var originalSalt = user.Salt;
        var newPassword = "newpassword123";

        // Act
        user.UpdatePassword(newPassword);

        // Assert
        user.Password.Should().NotBe(originalPassword);
        user.Salt.Should().NotBe(originalSalt);
        user.VerifyPassword(newPassword).Should().BeTrue();
    }

    [Fact]
    public void User_GetFullName_ShouldReturnCorrectFormat()
    {
        // Arrange
        var user = new User("test@example.com", "John", "Doe", "password", "salt");

        // Act
        var fullName = user.GetFullName();

        // Assert
        fullName.Should().Be("John Doe");
    }

    [Theory]
    [InlineData('M')]
    [InlineData('F')]
    [InlineData('O')]
    public void User_Gender_ShouldAcceptValidGenderValues(char gender)
    {
        // Arrange & Act
        var user = new User
        {
            Gender = gender
        };

        // Assert
        user.Gender.Should().Be(gender);
    }

    [Fact]
    public void User_DefaultConstructor_ShouldGenerateSalt()
    {
        // Arrange & Act
        var user = new User();

        // Assert
        user.Salt.Should().NotBeNullOrEmpty();
        user.Active.Should().BeTrue();
        user.UserBusinesses.Should().NotBeNull();
    }
}
