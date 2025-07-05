using FluentAssertions;
using GPInventory.Application.DTOs.Auth;
using GPInventory.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;

namespace GPInventory.IntegrationTests;

public class AuthControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AuthControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the real database context
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add in-memory database for testing
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb");
                });

                // Build the service provider
                var serviceProvider = services.BuildServiceProvider();

                // Create a scope to obtain a reference to the database context
                using var scope = serviceProvider.CreateScope();
                var scopedServices = scope.ServiceProvider;
                var db = scopedServices.GetRequiredService<ApplicationDbContext>();
                var logger = scopedServices.GetRequiredService<ILogger<AuthControllerIntegrationTests>>();

                // Ensure the database is created
                db.Database.EnsureCreated();

                try
                {
                    // Seed the database with test data
                    DataSeeder.SeedAsync(db).Wait();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred seeding the database with test data.");
                }
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidData_ShouldReturnSuccessAndToken()
    {
        // Arrange
        var registerDto = new RegisterDto
        {
            Email = "integration@test.com",
            Name = "Integration",
            LastName = "Test",
            Password = "password123",
            Gender = 'M',
            Phone = 123456789
        };

        var json = JsonConvert.SerializeObject(registerDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/auth/register", content);

        // Assert
        response.Should().BeSuccessful();
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var authResponse = JsonConvert.DeserializeObject<AuthResponseDto>(responseContent);
        
        authResponse.Should().NotBeNull();
        authResponse!.Token.Should().NotBeNullOrEmpty();
        authResponse.User.Should().NotBeNull();
        authResponse.User.Email.Should().Be(registerDto.Email);
        authResponse.User.Name.Should().Be(registerDto.Name);
        authResponse.User.LastName.Should().Be(registerDto.LastName);
    }

    [Fact]
    public async Task Register_WithExistingEmail_ShouldReturnBadRequest()
    {
        // Arrange
        var registerDto = new RegisterDto
        {
            Email = "existing@test.com",
            Name = "Existing",
            LastName = "User",
            Password = "password123"
        };

        var json = JsonConvert.SerializeObject(registerDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // First registration
        await _client.PostAsync("/api/auth/register", content);

        // Act - Second registration with same email
        var response = await _client.PostAsync("/api/auth/register", content);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnSuccessAndToken()
    {
        // Arrange
        var registerDto = new RegisterDto
        {
            Email = "login@test.com",
            Name = "Login",
            LastName = "Test",
            Password = "password123"
        };

        var registerJson = JsonConvert.SerializeObject(registerDto);
        var registerContent = new StringContent(registerJson, Encoding.UTF8, "application/json");

        // First register a user
        await _client.PostAsync("/api/auth/register", registerContent);

        var loginDto = new LoginDto
        {
            Email = registerDto.Email,
            Password = registerDto.Password
        };

        var loginJson = JsonConvert.SerializeObject(loginDto);
        var loginContent = new StringContent(loginJson, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/auth/login", loginContent);

        // Assert
        response.Should().BeSuccessful();
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var authResponse = JsonConvert.DeserializeObject<AuthResponseDto>(responseContent);
        
        authResponse.Should().NotBeNull();
        authResponse!.Token.Should().NotBeNullOrEmpty();
        authResponse.User.Should().NotBeNull();
        authResponse.User.Email.Should().Be(loginDto.Email);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldReturnUnauthorized()
    {
        // Arrange
        var loginDto = new LoginDto
        {
            Email = "nonexistent@test.com",
            Password = "wrongpassword"
        };

        var json = JsonConvert.SerializeObject(loginDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/auth/login", content);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ValidateToken_WithValidToken_ShouldReturnSuccess()
    {
        // Arrange
        var registerDto = new RegisterDto
        {
            Email = "validate@test.com",
            Name = "Validate",
            LastName = "Test",
            Password = "password123"
        };

        var registerJson = JsonConvert.SerializeObject(registerDto);
        var registerContent = new StringContent(registerJson, Encoding.UTF8, "application/json");

        // Register and get token
        var registerResponse = await _client.PostAsync("/api/auth/register", registerContent);
        var registerResponseContent = await registerResponse.Content.ReadAsStringAsync();
        var authResponse = JsonConvert.DeserializeObject<AuthResponseDto>(registerResponseContent);

        // Set authorization header
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

        // Act
        var response = await _client.PostAsync("/api/auth/validate-token", null);

        // Assert
        response.Should().BeSuccessful();
    }

    [Fact]
    public async Task ValidateToken_WithoutToken_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.PostAsync("/api/auth/validate-token", null);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentUser_WithValidToken_ShouldReturnUserInfo()
    {
        // Arrange
        var registerDto = new RegisterDto
        {
            Email = "getuser@test.com",
            Name = "GetUser",
            LastName = "Test",
            Password = "password123"
        };

        var registerJson = JsonConvert.SerializeObject(registerDto);
        var registerContent = new StringContent(registerJson, Encoding.UTF8, "application/json");

        // Register and get token
        var registerResponse = await _client.PostAsync("/api/auth/register", registerContent);
        var registerResponseContent = await registerResponse.Content.ReadAsStringAsync();
        var authResponse = JsonConvert.DeserializeObject<AuthResponseDto>(registerResponseContent);

        // Set authorization header
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        response.Should().BeSuccessful();
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var userDto = JsonConvert.DeserializeObject<UserDto>(responseContent);
        
        userDto.Should().NotBeNull();
        userDto!.Email.Should().Be(registerDto.Email);
        userDto.Name.Should().Be(registerDto.Name);
        userDto.LastName.Should().Be(registerDto.LastName);
    }
}
