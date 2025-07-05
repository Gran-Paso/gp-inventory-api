using FluentAssertions;
using GPInventory.Domain.Entities;

namespace GPInventory.Tests.Domain;

public class StockTests
{
    [Fact]
    public void Stock_Constructor_ShouldCreateStockWithCorrectProperties()
    {
        // Arrange
        var productId = 1;
        var flowId = 1;
        var amount = 10;
        var auctionPrice = 100;

        // Act
        var stock = new Stock(productId, flowId, amount, auctionPrice);

        // Assert
        stock.ProductId.Should().Be(productId);
        stock.FlowId.Should().Be(flowId);
        stock.Amount.Should().Be(amount);
        stock.AuctionPrice.Should().Be(auctionPrice);
        stock.Date.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Stock_Constructor_WithoutAuctionPrice_ShouldWork()
    {
        // Arrange
        var productId = 1;
        var flowId = 1;
        var amount = 10;

        // Act
        var stock = new Stock(productId, flowId, amount);

        // Assert
        stock.ProductId.Should().Be(productId);
        stock.FlowId.Should().Be(flowId);
        stock.Amount.Should().Be(amount);
        stock.AuctionPrice.Should().BeNull();
    }

    [Fact]
    public void Stock_DefaultConstructor_ShouldSetDate()
    {
        // Arrange & Act
        var stock = new Stock();

        // Assert
        stock.Date.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
