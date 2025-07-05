using FluentAssertions;
using GPInventory.Domain.Entities;
using GPInventory.Infrastructure.Data;
using GPInventory.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GPInventory.Tests.Infrastructure;

public class StockRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly StockRepository _stockRepository;

    public StockRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _stockRepository = new StockRepository(_context);
    }

    [Fact]
    public async Task GetByProductIdAsync_WithStocks_ShouldReturnStocksOrderedByDate()
    {
        // Arrange
        var business = new Business("Test Business", 1, "#FF0000");
        var productType = new ProductType("Electronics", "Electronic devices");
        var flowTypeIn = new FlowType("entrada");
        var flowTypeOut = new FlowType("salida");

        await _context.Businesses.AddAsync(business);
        await _context.ProductTypes.AddAsync(productType);
        await _context.FlowTypes.AddRangeAsync(flowTypeIn, flowTypeOut);
        await _context.SaveChangesAsync();

        var product = new Product("Test Product", productType.Id, 100, 50, business.Id);
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        var stock1 = new Stock(product.Id, flowTypeIn.Id, 10) { Date = DateTime.UtcNow.AddDays(-2) };
        var stock2 = new Stock(product.Id, flowTypeOut.Id, 5) { Date = DateTime.UtcNow.AddDays(-1) };
        var stock3 = new Stock(product.Id, flowTypeIn.Id, 15) { Date = DateTime.UtcNow };

        await _context.Stocks.AddRangeAsync(stock1, stock2, stock3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _stockRepository.GetByProductIdAsync(product.Id);

        // Assert
        result.Should().HaveCount(3);
        var resultList = result.ToList();
        resultList[0].Date.Should().BeAfter(resultList[1].Date); // Ordered by date descending
        resultList[1].Date.Should().BeAfter(resultList[2].Date);
        result.All(s => s.Flow != null).Should().BeTrue();
    }

    [Fact]
    public async Task GetCurrentStockAsync_WithEntradasAndSalidas_ShouldCalculateCorrectStock()
    {
        // Arrange
        var business = new Business("Test Business", 1, "#FF0000");
        var productType = new ProductType("Electronics", "Electronic devices");
        var flowTypeIn = new FlowType("entrada");
        var flowTypeOut = new FlowType("salida");

        await _context.Businesses.AddAsync(business);
        await _context.ProductTypes.AddAsync(productType);
        await _context.FlowTypes.AddRangeAsync(flowTypeIn, flowTypeOut);
        await _context.SaveChangesAsync();

        var product = new Product("Test Product", productType.Id, 100, 50, business.Id);
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        // Add stocks: +50, -20, +30, -10 = 50 total
        var stocks = new[]
        {
            new Stock(product.Id, flowTypeIn.Id, 50),   // +50
            new Stock(product.Id, flowTypeOut.Id, 20),  // -20
            new Stock(product.Id, flowTypeIn.Id, 30),   // +30
            new Stock(product.Id, flowTypeOut.Id, 10)   // -10
        };

        await _context.Stocks.AddRangeAsync(stocks);
        await _context.SaveChangesAsync();

        // Act
        var result = await _stockRepository.GetCurrentStockAsync(product.Id);

        // Assert
        result.Should().Be(50); // 50 + 30 - 20 - 10 = 50
    }

    [Fact]
    public async Task GetCurrentStockAsync_WithNoStocks_ShouldReturnZero()
    {
        // Arrange
        var business = new Business("Test Business", 1, "#FF0000");
        var productType = new ProductType("Electronics", "Electronic devices");

        await _context.Businesses.AddAsync(business);
        await _context.ProductTypes.AddAsync(productType);
        await _context.SaveChangesAsync();

        var product = new Product("Test Product", productType.Id, 100, 50, business.Id);
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        // Act
        var result = await _stockRepository.GetCurrentStockAsync(product.Id);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task GetCurrentStockAsync_WithOnlyEntradas_ShouldReturnPositiveStock()
    {
        // Arrange
        var business = new Business("Test Business", 1, "#FF0000");
        var productType = new ProductType("Electronics", "Electronic devices");
        var flowTypeIn = new FlowType("entrada");

        await _context.Businesses.AddAsync(business);
        await _context.ProductTypes.AddAsync(productType);
        await _context.FlowTypes.AddAsync(flowTypeIn);
        await _context.SaveChangesAsync();

        var product = new Product("Test Product", productType.Id, 100, 50, business.Id);
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        var stocks = new[]
        {
            new Stock(product.Id, flowTypeIn.Id, 25),
            new Stock(product.Id, flowTypeIn.Id, 75)
        };

        await _context.Stocks.AddRangeAsync(stocks);
        await _context.SaveChangesAsync();

        // Act
        var result = await _stockRepository.GetCurrentStockAsync(product.Id);

        // Assert
        result.Should().Be(100); // 25 + 75 = 100
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
