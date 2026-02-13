using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Unit tests for GetCollectionStatusQuery creation.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class GetCollectionStatusQueryTests
{
    [Fact]
    public void GetCollectionStatusQuery_CreatesWithRequiredProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var entityType = EntityType.Event;

        // Act
        var query = new GetCollectionStatusQuery(userId, entityType, entityId);

        // Assert
        query.UserId.Should().Be(userId);
        query.EntityType.Should().Be(entityType);
        query.EntityId.Should().Be(entityId);
    }

    [Theory]
    [InlineData(EntityType.Game)]
    [InlineData(EntityType.Player)]
    [InlineData(EntityType.Event)]
    [InlineData(EntityType.Session)]
    [InlineData(EntityType.Agent)]
    [InlineData(EntityType.Document)]
    [InlineData(EntityType.ChatSession)]
    public void GetCollectionStatusQuery_SupportsAllEntityTypes(EntityType entityType)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        // Act
        var query = new GetCollectionStatusQuery(userId, entityType, entityId);

        // Assert
        query.EntityType.Should().Be(entityType);
    }

    [Fact]
    public void GetCollectionStatusQuery_WithDifferentEntities_CreatesDistinctQueries()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entityId1 = Guid.NewGuid();
        var entityId2 = Guid.NewGuid();

        // Act
        var query1 = new GetCollectionStatusQuery(userId, EntityType.Player, entityId1);
        var query2 = new GetCollectionStatusQuery(userId, EntityType.Player, entityId2);

        // Assert
        query1.EntityId.Should().NotBe(query2.EntityId);
        query1.UserId.Should().Be(query2.UserId);
    }
}
