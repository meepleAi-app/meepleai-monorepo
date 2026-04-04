using Api.Infrastructure.Entities.SharedGameCatalog;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

/// <summary>
/// Verifica che SharedGameEntity abbia RowVersion per ottimistic concurrency.
/// Spec-panel recommendation C-3.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SharedGameEntityRowVersionTests
{
    [Fact]
    public void SharedGameEntity_HasRowVersionProperty()
    {
        // Arrange & Act
        var property = typeof(SharedGameEntity).GetProperty("RowVersion");

        // Assert
        property.Should().NotBeNull("SharedGameEntity must have a RowVersion property for optimistic concurrency");
        property!.PropertyType.Should().Be(typeof(byte[]), "RowVersion must be byte[] for EF Core timestamp token");
    }

    [Fact]
    public void SharedGameEntity_RowVersionIsNullableByDefault()
    {
        // Arrange & Act
        var entity = new SharedGameEntity();

        // Assert
        entity.RowVersion.Should().BeNull("new entity RowVersion is assigned by the database on first save");
    }
}
