using FluentAssertions;
using GPInventory.Domain.Entities;

namespace GPInventory.Tests.Domain;

public class BusinessTests
{
    [Fact]
    public void Business_Constructor_ShouldCreateBusinessWithCorrectProperties()
    {
        // Arrange
        var companyName = "Test Company";
        var theme = 1;
        var primaryColor = "#FF0000";

        // Act
        var business = new Business(companyName, theme, primaryColor);

        // Assert
        business.CompanyName.Should().Be(companyName);
        business.Theme.Should().Be(theme);
        business.PrimaryColor.Should().Be(primaryColor);
        business.UserBusinesses.Should().NotBeNull();
        business.UserBusinesses.Should().BeEmpty();
        business.Products.Should().NotBeNull();
        business.Products.Should().BeEmpty();
    }

    [Fact]
    public void Business_DefaultConstructor_ShouldInitializeCollections()
    {
        // Arrange & Act
        var business = new Business();

        // Assert
        business.UserBusinesses.Should().NotBeNull();
        business.Products.Should().NotBeNull();
        business.CompanyName.Should().BeEmpty();
    }

    [Fact]
    public void Business_Constructor_WithMinimalParameters_ShouldWork()
    {
        // Arrange
        var companyName = "Minimal Company";

        // Act
        var business = new Business(companyName);

        // Assert
        business.CompanyName.Should().Be(companyName);
        business.Theme.Should().BeNull();
        business.PrimaryColor.Should().BeNull();
    }
}
