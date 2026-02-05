using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers.PrivateGames;

/// <summary>
/// Unit tests for DeletePrivateGameCommand.
/// Issue #3663: Phase 2 - Private Game CRUD Operations.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class DeletePrivateGameCommandHandlerTests
{
    [Fact]
    public void DeletePrivateGameCommand_CreatesWithRequiredProperties()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var command = new DeletePrivateGameCommand(gameId, userId);

        // Assert
        command.PrivateGameId.Should().Be(gameId);
        command.UserId.Should().Be(userId);
    }
}
