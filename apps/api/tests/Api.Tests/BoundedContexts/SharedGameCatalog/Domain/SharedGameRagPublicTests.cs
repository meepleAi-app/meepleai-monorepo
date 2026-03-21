using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

/// <summary>
/// Tests for the IsRagPublic property and SetRagPublicAccess method on SharedGame.
/// Controls whether RAG access requires ownership declaration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SharedGameRagPublicTests
{
    private static readonly Guid TestUserId = Guid.NewGuid();

    [Fact]
    public void IsRagPublic_DefaultsToFalse()
    {
        // Arrange & Act
        var game = CreateSharedGame();

        // Assert
        game.IsRagPublic.Should().BeFalse();
    }

    [Fact]
    public void SetRagPublicAccess_True_SetsIsRagPublicAndUpdatesModifiedAt()
    {
        // Arrange
        var game = CreateSharedGame();
        var beforeModified = game.ModifiedAt;

        // Act
        game.SetRagPublicAccess(true);

        // Assert
        game.IsRagPublic.Should().BeTrue();
        game.ModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        game.ModifiedAt.Should().NotBe(beforeModified);
    }

    #region Helpers

    private static SharedGame CreateSharedGame()
    {
        return SharedGame.Create(
            title: "Test Game",
            yearPublished: 2024,
            description: "A test game for RAG access tests",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m,
            averageRating: 7.0m,
            imageUrl: "https://example.com/test.jpg",
            thumbnailUrl: "https://example.com/test-thumb.jpg",
            rules: GameRules.Create("Test rules", "en"),
            createdBy: TestUserId);
    }

    #endregion
}
