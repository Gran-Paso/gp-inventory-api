using FluentAssertions;
using GPInventory.Domain.Entities;

namespace GPInventory.Tests.Domain;

public class ProductTypeTests
{
    [Fact]
    public void ProductType_Constructor_ShouldCreateProductTypeWithCorrectProperties()
    {
        // Arrange
        var name = "Electronics";
        var description = "Electronic devices";

        // Act
        var productType = new ProductType(name, description);

        // Assert
        productType.Name.Should().Be(name);
        productType.Description.Should().Be(description);
        productType.Products.Should().NotBeNull();
        productType.Products.Should().BeEmpty();
    }

    [Fact]
    public void ProductType_Constructor_WithoutDescription_ShouldWork()
    {
        // Arrange
        var name = "Books";

        // Act
        var productType = new ProductType(name);

        // Assert
        productType.Name.Should().Be(name);
        productType.Description.Should().BeNull();
    }

    [Fact]
    public void ProductType_DefaultConstructor_ShouldInitializeCollections()
    {
        // Arrange & Act
        var productType = new ProductType();

        // Assert
        productType.Products.Should().NotBeNull();
        productType.Name.Should().BeEmpty();
    }
}
