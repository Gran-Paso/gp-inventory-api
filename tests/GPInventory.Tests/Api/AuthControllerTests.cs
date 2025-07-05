using FluentAssertions;
using GPInventory.Api.Controllers;
using GPInventory.Application.DTOs.Auth;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace GPInventory.Tests.Api;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _authServiceMock = new Mock<IAuthService>();
        _loggerMock = new Mock<ILogger<AuthController>>();
        _controller = new AuthController(_authServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnOkResult()
    {
        // Arrange
        var loginDto = new LoginDto
        {
            Email = "test@example.com",
            Password = "password123"
        };

        var authResponse = new AuthResponseDto
        {
            Token = "fake-token",
            User = new UserDto
            {
                Id = 1,
                Email = "test@example.com",
                Name = "John",
                LastName = "Doe",
                Active = true
            }
        };

        _authServiceMock.Setup(x => x.LoginAsync(loginDto))
            .ReturnsAsync(authResponse);

        // Act
        var result = await _controller.Login(loginDto);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(authResponse);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldReturnUnauthorized()
    {
        // Arrange
        var loginDto = new LoginDto
        {
            Email = "test@example.com",
            Password = "wrongpassword"
        };

        _authServiceMock.Setup(x => x.LoginAsync(loginDto))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid credentials"));

        // Act
        var result = await _controller.Login(loginDto);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().BeEquivalentTo(new { message = "Invalid credentials" });
    }

    [Fact]
    public async Task Login_WithException_ShouldReturnInternalServerError()
    {
        // Arrange
        var loginDto = new LoginDto
        {
            Email = "test@example.com",
            Password = "password123"
        };

        _authServiceMock.Setup(x => x.LoginAsync(loginDto))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.Login(loginDto);

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);
        statusCodeResult.Value.Should().BeEquivalentTo(new { message = "An error occurred during login" });
    }

    [Fact]
    public async Task Register_WithValidData_ShouldReturnOkResult()
    {
        // Arrange
        var registerDto = new RegisterDto
        {
            Email = "newuser@example.com",
            Name = "Jane",
            LastName = "Smith",
            Password = "password123"
        };

        var authResponse = new AuthResponseDto
        {
            Token = "fake-token",
            User = new UserDto
            {
                Id = 1,
                Email = "newuser@example.com",
                Name = "Jane",
                LastName = "Smith",
                Active = true
            }
        };

        _authServiceMock.Setup(x => x.RegisterAsync(registerDto))
            .ReturnsAsync(authResponse);

        // Act
        var result = await _controller.Register(registerDto);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(authResponse);
    }

    [Fact]
    public async Task Register_WithExistingEmail_ShouldReturnBadRequest()
    {
        // Arrange
        var registerDto = new RegisterDto
        {
            Email = "existing@example.com",
            Name = "Jane",
            LastName = "Smith",
            Password = "password123"
        };

        _authServiceMock.Setup(x => x.RegisterAsync(registerDto))
            .ThrowsAsync(new InvalidOperationException("User with this email already exists"));

        // Act
        var result = await _controller.Register(registerDto);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().BeEquivalentTo(new { message = "User with this email already exists" });
    }

    [Fact]
    public async Task Register_WithException_ShouldReturnInternalServerError()
    {
        // Arrange
        var registerDto = new RegisterDto
        {
            Email = "newuser@example.com",
            Name = "Jane",
            LastName = "Smith",
            Password = "password123"
        };

        _authServiceMock.Setup(x => x.RegisterAsync(registerDto))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.Register(registerDto);

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);
        statusCodeResult.Value.Should().BeEquivalentTo(new { message = "An error occurred during registration" });
    }

    [Fact]
    public async Task ValidateToken_WithValidToken_ShouldReturnOkResult()
    {
        // Arrange
        var token = "valid-token";
        
        // Setup the Authorization header
        _controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };
        _controller.HttpContext.Request.Headers["Authorization"] = $"Bearer {token}";

        _authServiceMock.Setup(x => x.ValidateTokenAsync(token))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.ValidateToken();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(new { message = "Token is valid", valid = true });
    }

    [Fact]
    public async Task ValidateToken_WithInvalidToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var token = "invalid-token";
        
        // Setup the Authorization header
        _controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };
        _controller.HttpContext.Request.Headers["Authorization"] = $"Bearer {token}";

        _authServiceMock.Setup(x => x.ValidateTokenAsync(token))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.ValidateToken();

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().BeEquivalentTo(new { message = "Invalid token" });
    }

    [Fact]
    public async Task ValidateToken_WithoutToken_ShouldReturnUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };

        // Act
        var result = await _controller.ValidateToken();

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.Value.Should().BeEquivalentTo(new { message = "Token is required" });
    }
}
