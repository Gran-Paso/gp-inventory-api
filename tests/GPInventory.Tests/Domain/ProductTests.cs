using FluentAssertions;
using GPInventory.Domain.Entities;

namespace GPInventory.Tests.Domain;

public class ProductTests
{
    [Fact]
    public void Product_Constructor_ShouldCreateProductWithCorrectProperties()
    {
        // Arrange
        var name = "Test Product";
        var productTypeId = 1;
        var price = 100;
        var cost = 50;
        var businessId = 1;

        // Act
        var product = new Product(name, productTypeId, price, cost, businessId);

        // Assert
        product.Name.Should().Be(name);
        product.ProductTypeId.Should().Be(productTypeId);
        product.Price.Should().Be(price);
        product.Cost.Should().Be(cost);
        product.BusinessId.Should().Be(businessId);
        product.Date.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        product.Stocks.Should().NotBeNull();
        product.Stocks.Should().BeEmpty();
    }

    [Fact]
    public void Product_DefaultConstructor_ShouldInitializeCollections()
    {
        // Arrange & Act
        var product = new Product();

        // Assert
        product.Stocks.Should().NotBeNull();
        product.Date.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        product.Name.Should().BeEmpty();
    }

    [Fact]
    public void Product_Properties_ShouldBeSettable()
    {
        // Arrange
        var product = new Product();
        var image = "image.jpg";
        var sku = "SKU001";

        // Act
        product.Image = image;
        product.Sku = sku;

        // Assert
        product.Image.Should().Be(image);
        product.Sku.Should().Be(sku);
    }
}
