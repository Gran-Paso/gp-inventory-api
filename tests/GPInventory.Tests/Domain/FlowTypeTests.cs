using FluentAssertions;
using GPInventory.Domain.Entities;

namespace GPInventory.Tests.Domain;

public class FlowTypeTests
{
    [Fact]
    public void FlowType_Constructor_ShouldCreateFlowTypeWithCorrectProperties()
    {
        // Arrange
        var type = "entrada";

        // Act
        var flowType = new FlowType(type);

        // Assert
        flowType.Type.Should().Be(type);
        flowType.Stocks.Should().NotBeNull();
        flowType.Stocks.Should().BeEmpty();
    }

    [Fact]
    public void FlowType_DefaultConstructor_ShouldInitializeCollections()
    {
        // Arrange & Act
        var flowType = new FlowType();

        // Assert
        flowType.Stocks.Should().NotBeNull();
        flowType.Type.Should().BeEmpty();
    }

    [Theory]
    [InlineData("entrada")]
    [InlineData("salida")]
    public void FlowType_Constructor_ShouldAcceptValidTypes(string type)
    {
        // Arrange & Act
        var flowType = new FlowType(type);

        // Assert
        flowType.Type.Should().Be(type);
    }
}
