using FluentAssertions;
using GPInventory.Domain.Entities;

namespace GPInventory.Tests.Domain;

public class UserHasBusinessTests
{
    [Fact]
    public void UserHasBusiness_Constructor_ShouldInitializeProperties()
    {
        // Arrange
        var userId = 1;
        var businessId = 2;
        var roleId = 3;

        // Act
        var userHasBusiness = new UserHasBusiness(userId, businessId, roleId);

        // Assert
        userHasBusiness.UserId.Should().Be(userId);
        userHasBusiness.BusinessId.Should().Be(businessId);
        userHasBusiness.RoleId.Should().Be(roleId);
    }

    [Fact]
    public void UserHasBusiness_DefaultConstructor_ShouldWork()
    {
        // Arrange & Act
        var userHasBusiness = new UserHasBusiness();

        // Assert
        userHasBusiness.UserId.Should().Be(0);
        userHasBusiness.BusinessId.Should().Be(0);
        userHasBusiness.RoleId.Should().Be(0);
    }

    [Fact]
    public void UserHasBusiness_Properties_ShouldBeSettable()
    {
        // Arrange
        var userHasBusiness = new UserHasBusiness();
        var user = new User("test@example.com", "Test", "User", "password", "salt");
        var business = new Business("Test Business");
        var role = new Role("Test Role");

        // Act
        userHasBusiness.User = user;
        userHasBusiness.Business = business;
        userHasBusiness.Role = role;

        // Assert
        userHasBusiness.User.Should().Be(user);
        userHasBusiness.Business.Should().Be(business);
        userHasBusiness.Role.Should().Be(role);
    }
}
