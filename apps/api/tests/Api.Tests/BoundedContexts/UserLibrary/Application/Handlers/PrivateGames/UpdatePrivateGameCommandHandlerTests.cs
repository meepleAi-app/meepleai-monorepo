using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers.PrivateGames;

/// <summary>
/// Unit tests for UpdatePrivateGameCommand.
/// Issue #3663: Phase 2 - Private Game CRUD Operations.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class UpdatePrivateGameCommandHandlerTests
{
    [Fact]
    public void UpdatePrivateGameCommand_CreatesWithAllProperties()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var command = new UpdatePrivateGameCommand(
            PrivateGameId: gameId,
            UserId: userId,
            Title: "Updated Title",
            MinPlayers: 3,
            MaxPlayers: 6,
            YearPublished: 2024,
            Description: "Updated description",
            PlayingTimeMinutes: 90,
            MinAge: 12,
            ComplexityRating: 3.0m,
            ImageUrl: "https://example.com/updated.jpg"
        );

        // Assert
        command.PrivateGameId.Should().Be(gameId);
        command.UserId.Should().Be(userId);
        command.Title.Should().Be("Updated Title");
        command.MinPlayers.Should().Be(3);
        command.MaxPlayers.Should().Be(6);
    }
}
