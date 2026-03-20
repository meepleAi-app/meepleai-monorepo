using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

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
        game.Status.Should().Be(GameStatus.Published);
        game.ModifiedBy.Should().Be(TestUserId);
        game.ModifiedAt.Should().NotBeNull();
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
        game.DomainEvents.Should().ContainSingle();
        var publishedEvent = game.DomainEvents.First().Should().BeOfType<SharedGameQuickPublishedEvent>().Subject;
        publishedEvent.GameId.Should().Be(game.Id);
        publishedEvent.PublishedBy.Should().Be(TestUserId);
    }

    [Fact]
    public void QuickPublish_OnPendingApprovalGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateDraftGame();
        game.SubmitForApproval(TestUserId); // Draft → PendingApproval

        // Act & Assert
        var act = () => game.QuickPublish(TestUserId);
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("PendingApproval");
        exception.Message.Should().Contain("Only Draft games can be quick-published");
    }

    [Fact]
    public void QuickPublish_WithEmptyGuid_ThrowsArgumentException()
    {
        // Arrange
        var game = CreateDraftGame();

        // Act & Assert
        var act = () => game.QuickPublish(Guid.Empty);
        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.ParamName.Should().Be("publishedBy");
    }

    [Fact]
    public void QuickPublish_OnPublishedGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateDraftGame();
        game.QuickPublish(TestUserId); // Draft → Published

        // Act & Assert
        var act = () => game.QuickPublish(Guid.NewGuid());
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("Published");
    }

    [Fact]
    public void QuickPublish_OnArchivedGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateDraftGame();
        game.QuickPublish(TestUserId); // Draft → Published
        game.Archive(TestUserId);       // Published → Archived

        // Act & Assert
        var act = () => game.QuickPublish(Guid.NewGuid());
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("Archived");
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