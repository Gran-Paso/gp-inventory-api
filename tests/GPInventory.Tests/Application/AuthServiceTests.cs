using AutoMapper;
using FluentAssertions;
using GPInventory.Application.DTOs.Auth;
using GPInventory.Application.Interfaces;
using GPInventory.Application.Services;
using GPInventory.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace GPInventory.Tests.Application;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IPasswordService> _passwordServiceMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _tokenServiceMock = new Mock<ITokenService>();
        _passwordServiceMock = new Mock<IPasswordService>();
        _mapperMock = new Mock<IMapper>();
        
        _authService = new AuthService(
            _userRepositoryMock.Object,
            _tokenServiceMock.Object,
            _passwordServiceMock.Object,
            _mapperMock.Object);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ShouldReturnAuthResponse()
    {
        // Arrange
        var loginDto = new LoginDto
        {
            Email = "test@example.com",
            Password = "password123"
        };

        var user = new User("test@example.com", "John", "Doe", "hashedpassword", "salt")
        {
            Active = true
        };

        var userDto = new UserDto
        {
            Id = 1,
            Email = "test@example.com",
            Name = "John",
            LastName = "Doe",
            Active = true
        };

        var token = "fake-jwt-token";

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(loginDto.Email))
            .ReturnsAsync(user);
        
        _mapperMock.Setup(x => x.Map<UserDto>(user))
            .Returns(userDto);
        
        _tokenServiceMock.Setup(x => x.GenerateToken(userDto))
            .Returns(token);

        // Act
        var result = await _authService.LoginAsync(loginDto);

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().Be(token);
        result.User.Should().Be(userDto);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidEmail_ShouldThrowUnauthorizedException()
    {
        // Arrange
        var loginDto = new LoginDto
        {
            Email = "invalid@example.com",
            Password = "password123"
        };

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(loginDto.Email))
            .ReturnsAsync((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
            _authService.LoginAsync(loginDto));
    }

    [Fact]
    public async Task LoginAsync_WithInactiveUser_ShouldThrowUnauthorizedException()
    {
        // Arrange
        var loginDto = new LoginDto
        {
            Email = "test@example.com",
            Password = "password123"
        };

        var user = new User("test@example.com", "John", "Doe", "hashedpassword", "salt")
        {
            Active = false
        };

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(loginDto.Email))
            .ReturnsAsync(user);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
            _authService.LoginAsync(loginDto));
    }

    [Fact]
    public async Task RegisterAsync_WithValidData_ShouldReturnAuthResponse()
    {
        // Arrange
        var registerDto = new RegisterDto
        {
            Email = "newuser@example.com",
            Name = "Jane",
            LastName = "Smith",
            Password = "password123"
        };

        var salt = "generatedsalt";
        var hashedPassword = "hashedpassword";
        var token = "fake-jwt-token";

        var userDto = new UserDto
        {
            Id = 1,
            Email = registerDto.Email,
            Name = registerDto.Name,
            LastName = registerDto.LastName,
            Active = true
        };

        _userRepositoryMock.Setup(x => x.ExistsAsync(registerDto.Email))
            .ReturnsAsync(false);
        
        _passwordServiceMock.Setup(x => x.GenerateSalt())
            .Returns(salt);
        
        _passwordServiceMock.Setup(x => x.HashPassword(registerDto.Password, salt))
            .Returns(hashedPassword);
        
        _userRepositoryMock.Setup(x => x.AddAsync(It.IsAny<User>()))
            .ReturnsAsync((User user) => user);
        
        _userRepositoryMock.Setup(x => x.SaveChangesAsync())
            .ReturnsAsync(1);
        
        _mapperMock.Setup(x => x.Map<UserDto>(It.IsAny<User>()))
            .Returns(userDto);
        
        _tokenServiceMock.Setup(x => x.GenerateToken(userDto))
            .Returns(token);

        // Act
        var result = await _authService.RegisterAsync(registerDto);

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().Be(token);
        result.User.Should().Be(userDto);
    }

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var registerDto = new RegisterDto
        {
            Email = "existing@example.com",
            Name = "Jane",
            LastName = "Smith",
            Password = "password123"
        };

        _userRepositoryMock.Setup(x => x.ExistsAsync(registerDto.Email))
            .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _authService.RegisterAsync(registerDto));
    }

    [Fact]
    public async Task ValidateTokenAsync_ShouldReturnTokenServiceResult()
    {
        // Arrange
        var token = "test-token";
        var expectedResult = true;

        _tokenServiceMock.Setup(x => x.ValidateToken(token))
            .Returns(expectedResult);

        // Act
        var result = await _authService.ValidateTokenAsync(token);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task GetUserByEmailAsync_WithExistingUser_ShouldReturnUserDto()
    {
        // Arrange
        var email = "test@example.com";
        var user = new User(email, "John", "Doe", "password", "salt");
        var userDto = new UserDto
        {
            Id = 1,
            Email = email,
            Name = "John",
            LastName = "Doe"
        };

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync(user);
        
        _mapperMock.Setup(x => x.Map<UserDto>(user))
            .Returns(userDto);

        // Act
        var result = await _authService.GetUserByEmailAsync(email);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(userDto);
    }

    [Fact]
    public async Task GetUserByEmailAsync_WithNonExistentUser_ShouldReturnNull()
    {
        // Arrange
        var email = "nonexistent@example.com";

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(email))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _authService.GetUserByEmailAsync(email);

        // Assert
        result.Should().BeNull();
    }
}
