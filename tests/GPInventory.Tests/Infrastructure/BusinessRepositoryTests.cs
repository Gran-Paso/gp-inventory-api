using FluentAssertions;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using GPInventory.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Tests.Infrastructure;

public class BusinessRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly BusinessRepository _businessRepository;
    private readonly UserRepository _userRepository;

    public BusinessRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _businessRepository = new BusinessRepository(_context);
        _userRepository = new UserRepository(_context);
    }

    [Fact]
    public async Task GetUserBusinessesAsync_WithUserBusinesses_ShouldReturnBusinesses()
    {
        // Arrange
        var user = new User("test@example.com", "John", "Doe", "password", "salt");
        var business1 = new Business("Business One", 1, "#FF0000");
        var business2 = new Business("Business Two", 2, "#00FF00");
        var role = new Role("Admin", "Administrator");

        await _context.Users.AddAsync(user);
        await _context.Businesses.AddRangeAsync(business1, business2);
        await _context.Roles.AddAsync(role);
        await _context.SaveChangesAsync();

        var userBusiness1 = new UserHasBusiness
        {
            UserId = user.Id,
            BusinessId = business1.Id,
            RoleId = role.Id
        };
        var userBusiness2 = new UserHasBusiness
        {
            UserId = user.Id,
            BusinessId = business2.Id,
            RoleId = role.Id
        };

        await _context.UserHasBusinesses.AddRangeAsync(userBusiness1, userBusiness2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _businessRepository.GetUserBusinessesAsync(user.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(b => b.CompanyName == "Business One");
        result.Should().Contain(b => b.CompanyName == "Business Two");
    }

    [Fact]
    public async Task GetUserBusinessesAsync_WithNoBusinesses_ShouldReturnEmpty()
    {
        // Arrange
        var user = new User("lonely@example.com", "Lonely", "User", "password", "salt");
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _businessRepository.GetUserBusinessesAsync(user.Id);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AddAsync_ShouldAddBusinessToDatabase()
    {
        // Arrange
        var business = new Business("New Business", 3, "#0000FF");

        // Act
        await _businessRepository.AddAsync(business);
        await _businessRepository.SaveChangesAsync();

        // Assert
        var savedBusiness = await _context.Businesses.FirstOrDefaultAsync(b => b.CompanyName == "New Business");
        savedBusiness.Should().NotBeNull();
        savedBusiness!.Theme.Should().Be(3);
        savedBusiness.PrimaryColor.Should().Be("#0000FF");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
