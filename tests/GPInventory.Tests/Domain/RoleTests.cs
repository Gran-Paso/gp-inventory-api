using FluentAssertions;
using GPInventory.Domain.Entities;

namespace GPInventory.Tests.Domain;

public class RoleTests
{
    [Fact]
    public void Role_Constructor_ShouldCreateRoleWithCorrectProperties()
    {
        // Arrange
        var name = "Admin";
        var description = "Administrator role";

        // Act
        var role = new Role(name, description);

        // Assert
        role.Name.Should().Be(name);
        role.Description.Should().Be(description);
        role.UserBusinesses.Should().NotBeNull();
        role.UserBusinesses.Should().BeEmpty();
    }

    [Fact]
    public void Role_Constructor_WithoutDescription_ShouldWork()
    {
        // Arrange
        var name = "User";

        // Act
        var role = new Role(name);

        // Assert
        role.Name.Should().Be(name);
        role.Description.Should().BeNull();
    }

    [Fact]
    public void Role_DefaultConstructor_ShouldInitializeCollections()
    {
        // Arrange & Act
        var role = new Role();

        // Assert
        role.UserBusinesses.Should().NotBeNull();
        role.Name.Should().BeEmpty();
    }
}
