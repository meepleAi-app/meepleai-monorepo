using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Unit tests for RemoveFromCollectionCommand.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class RemoveFromCollectionCommandTests
{
    [Fact]
    public void RemoveFromCollectionCommand_CreatesWithRequiredProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var entityType = EntityType.Player;

        // Act
        var command = new RemoveFromCollectionCommand(userId, entityType, entityId);

        // Assert
        command.UserId.Should().Be(userId);
        command.EntityType.Should().Be(entityType);
        command.EntityId.Should().Be(entityId);
    }

    [Theory]
    [InlineData(EntityType.Game)]
    [InlineData(EntityType.Player)]
    [InlineData(EntityType.Event)]
    [InlineData(EntityType.Session)]
    [InlineData(EntityType.Agent)]
    [InlineData(EntityType.Document)]
    [InlineData(EntityType.ChatSession)]
    public void RemoveFromCollectionCommand_SupportsAllEntityTypes(EntityType entityType)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        // Act
        var command = new RemoveFromCollectionCommand(userId, entityType, entityId);

        // Assert
        command.EntityType.Should().Be(entityType);
    }

    [Fact]
    public void RemoveFromCollectionCommand_WithDifferentUsers_CreatesDistinctCommands()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        // Act
        var command1 = new RemoveFromCollectionCommand(userId1, EntityType.Player, entityId);
        var command2 = new RemoveFromCollectionCommand(userId2, EntityType.Player, entityId);

        // Assert
        command1.UserId.Should().NotBe(command2.UserId);
        command1.EntityId.Should().Be(command2.EntityId);
    }
}
