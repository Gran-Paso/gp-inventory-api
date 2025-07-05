using FluentAssertions;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using GPInventory.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Tests.Infrastructure;

public class ProductRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ProductRepository _productRepository;

    public ProductRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _productRepository = new ProductRepository(_context);
    }

    [Fact]
    public async Task GetByBusinessIdAsync_WithProducts_ShouldReturnProductsWithProductType()
    {
        // Arrange
        var business = new Business("Test Business", 1, "#FF0000");
        var productType = new ProductType("Electronics", "Electronic devices");
        var product1 = new Product("Product 1", 1, 100, 50, 1);
        var product2 = new Product("Product 2", 1, 200, 100, 1);

        await _context.Businesses.AddAsync(business);
        await _context.ProductTypes.AddAsync(productType);
        await _context.SaveChangesAsync();

        product1.BusinessId = business.Id;
        product1.ProductTypeId = productType.Id;
        product2.BusinessId = business.Id;
        product2.ProductTypeId = productType.Id;

        await _context.Products.AddRangeAsync(product1, product2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _productRepository.GetByBusinessIdAsync(business.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.Name == "Product 1");
        result.Should().Contain(p => p.Name == "Product 2");
        result.All(p => p.ProductType != null).Should().BeTrue();
    }

    [Fact]
    public async Task GetBySkuAsync_WithExistingProduct_ShouldReturnProduct()
    {
        // Arrange
        var business = new Business("Test Business", 1, "#FF0000");
        var productType = new ProductType("Electronics", "Electronic devices");
        await _context.Businesses.AddAsync(business);
        await _context.ProductTypes.AddAsync(productType);
        await _context.SaveChangesAsync();

        var product = new Product("Test Product", productType.Id, 100, 50, business.Id)
        {
            Sku = "TEST001"
        };

        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        // Act
        var result = await _productRepository.GetBySkuAsync("TEST001");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Product");
        result.Sku.Should().Be("TEST001");
    }

    [Fact]
    public async Task GetBySkuAsync_WithNonExistentSku_ShouldReturnNull()
    {
        // Act
        var result = await _productRepository.GetBySkuAsync("NONEXISTENT");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByBusinessIdAsync_WithNonExistentBusiness_ShouldReturnEmpty()
    {
        // Act
        var result = await _productRepository.GetByBusinessIdAsync(999);

        // Assert
        result.Should().BeEmpty();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
