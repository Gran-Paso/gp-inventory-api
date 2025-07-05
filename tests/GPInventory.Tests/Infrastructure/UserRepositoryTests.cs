using FluentAssertions;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using GPInventory.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Tests.Infrastructure;

public class UserRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly UserRepository _userRepository;

    public UserRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _userRepository = new UserRepository(_context);
    }

    [Fact]
    public async Task GetByEmailAsync_WithExistingUser_ShouldReturnUser()
    {
        // Arrange
        var user = new User("test@example.com", "John", "Doe", "password", "salt");
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _userRepository.GetByEmailAsync("test@example.com");

        // Assert
        result.Should().NotBeNull();
        result!.Mail.Should().Be("test@example.com");
        result.Name.Should().Be("John");
        result.LastName.Should().Be("Doe");
    }

    [Fact]
    public async Task GetByEmailAsync_WithNonExistentUser_ShouldReturnNull()
    {
        // Act
        var result = await _userRepository.GetByEmailAsync("nonexistent@example.com");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_WithExistingUser_ShouldReturnTrue()
    {
        // Arrange
        var user = new User("existing@example.com", "Jane", "Smith", "password", "salt");
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _userRepository.ExistsAsync("existing@example.com");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistentUser_ShouldReturnFalse()
    {
        // Act
        var result = await _userRepository.ExistsAsync("nonexistent@example.com");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddAsync_ShouldAddUserToDatabase()
    {
        // Arrange
        var user = new User("newuser@example.com", "New", "User", "password", "salt");

        // Act
        await _userRepository.AddAsync(user);
        await _userRepository.SaveChangesAsync();

        // Assert
        var savedUser = await _context.Users.FirstOrDefaultAsync(u => u.Mail == "newuser@example.com");
        savedUser.Should().NotBeNull();
        savedUser!.Name.Should().Be("New");
        savedUser.LastName.Should().Be("User");
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingUser_ShouldReturnUser()
    {
        // Arrange
        var user = new User("getbyid@example.com", "Get", "ById", "password", "salt");
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _userRepository.GetByIdAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Mail.Should().Be("getbyid@example.com");
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllUsers()
    {
        // Arrange
        var users = new[]
        {
            new User("user1@example.com", "User", "One", "password", "salt"),
            new User("user2@example.com", "User", "Two", "password", "salt"),
            new User("user3@example.com", "User", "Three", "password", "salt")
        };

        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();

        // Act
        var result = await _userRepository.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(u => u.Mail == "user1@example.com");
        result.Should().Contain(u => u.Mail == "user2@example.com");
        result.Should().Contain(u => u.Mail == "user3@example.com");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
