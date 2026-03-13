using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class SharedGameQuickPublishTests
{
    private static readonly Guid TestUserId = Guid.NewGuid();

    [Fact]
    public void QuickPublish_OnDraftGame_SetsStatusToPublished()
    {
        // Arrange
        var game = CreateDraftGame();

        // Act
        game.QuickPublish(TestUserId);

        // Assert
        Assert.Equal(GameStatus.Published, game.Status);
        Assert.Equal(TestUserId, game.ModifiedBy);
        Assert.NotNull(game.ModifiedAt);
    }

    [Fact]
    public void QuickPublish_OnDraftGame_RaisesSharedGameQuickPublishedEvent()
    {
        // Arrange
        var game = CreateDraftGame();
        game.ClearDomainEvents(); // Clear the SharedGameCreatedEvent from Create()

        // Act
        game.QuickPublish(TestUserId);

        // Assert
        Assert.Single(game.DomainEvents);
        var publishedEvent = Assert.IsType<SharedGameQuickPublishedEvent>(game.DomainEvents.First());
        Assert.Equal(game.Id, publishedEvent.GameId);
        Assert.Equal(TestUserId, publishedEvent.PublishedBy);
    }

    [Fact]
    public void QuickPublish_OnPendingApprovalGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateDraftGame();
        game.SubmitForApproval(TestUserId); // Draft → PendingApproval

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => game.QuickPublish(TestUserId));
        Assert.Contains("PendingApproval", exception.Message);
        Assert.Contains("Only Draft games can be quick-published", exception.Message);
    }

    [Fact]
    public void QuickPublish_WithEmptyGuid_ThrowsArgumentException()
    {
        // Arrange
        var game = CreateDraftGame();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => game.QuickPublish(Guid.Empty));
        Assert.Equal("publishedBy", exception.ParamName);
    }

    [Fact]
    public void QuickPublish_OnPublishedGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateDraftGame();
        game.QuickPublish(TestUserId); // Draft → Published

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => game.QuickPublish(Guid.NewGuid()));
        Assert.Contains("Published", exception.Message);
    }

    [Fact]
    public void QuickPublish_OnArchivedGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateDraftGame();
        game.QuickPublish(TestUserId); // Draft → Published
        game.Archive(TestUserId);       // Published → Archived

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => game.QuickPublish(Guid.NewGuid()));
        Assert.Contains("Archived", exception.Message);
    }

    private static SharedGame CreateDraftGame()
    {
        return SharedGame.Create(
            title: "Test Game",
            yearPublished: 2020,
            description: "Test description",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 8,
            complexityRating: 2.0m,
            averageRating: 7.0m,
            imageUrl: "https://example.com/test.jpg",
            thumbnailUrl: "https://example.com/test-thumb.jpg",
            rules: GameRules.Create("Test rules", "en"),
            createdBy: TestUserId,
            bggId: null);
    }
}
