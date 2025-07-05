using FluentAssertions;
using GPInventory.Domain.Entities;

namespace GPInventory.Tests.Domain;

public class BaseEntityTests
{
    private class TestEntity : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void BaseEntity_Constructor_ShouldSetCreatedAt()
    {
        // Arrange & Act
        var entity = new TestEntity();

        // Assert
        entity.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        entity.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        entity.Id.Should().Be(0); // Default value for int
    }

    [Fact]
    public void BaseEntity_Properties_ShouldBeSettable()
    {
        // Arrange
        var entity = new TestEntity();
        var now = DateTime.UtcNow;

        // Act
        entity.Id = 123;
        entity.CreatedAt = now;
        entity.UpdatedAt = now;

        // Assert
        entity.Id.Should().Be(123);
        entity.CreatedAt.Should().Be(now);
        entity.UpdatedAt.Should().Be(now);
    }
}
