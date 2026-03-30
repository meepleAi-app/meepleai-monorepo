using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Tests for the Game aggregate root entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 29
/// </summary>
[Trait("Category", "Unit")]
public sealed class GameTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidTitle_CreatesGame()
    {
        // Arrange
        var id = Guid.NewGuid();
        var title = new GameTitle("Catan");

        // Act
        var game = new Game(id, title);

        // Assert
        game.Id.Should().Be(id);
        game.Title.Should().Be(title);
        game.Title.Value.Should().Be("Catan");
        game.Publisher.Should().BeNull();
        game.YearPublished.Should().BeNull();
        game.PlayerCount.Should().BeNull();
        game.PlayTime.Should().BeNull();
        game.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Constructor_WithAllParameters_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var title = new GameTitle("Wingspan");
        var publisher = new Publisher("Stonemaier Games");
        var yearPublished = new YearPublished(2019);
        var playerCount = new PlayerCount(1, 5);
        var playTime = new PlayTime(40, 70);

        // Act
        var game = new Game(id, title, publisher, yearPublished, playerCount, playTime);

        // Assert
        game.Title.Value.Should().Be("Wingspan");
        game.Publisher!.Name.Should().Be("Stonemaier Games");
        game.YearPublished!.Value.Should().Be(2019);
        game.PlayerCount!.Min.Should().Be(1);
        game.PlayerCount!.Max.Should().Be(5);
        game.PlayTime!.MinMinutes.Should().Be(40);
        game.PlayTime!.MaxMinutes.Should().Be(70);
    }

    [Fact]
    public void Constructor_WithNullTitle_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new Game(Guid.NewGuid(), null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("title");
    }

    [Fact]
    public void Constructor_AddsDomainEvent()
    {
        // Arrange
        var id = Guid.NewGuid();
        var title = new GameTitle("Azul");

        // Act
        var game = new Game(id, title);

        // Assert
        game.DomainEvents.Should().HaveCount(1);
        game.DomainEvents.First().Should().BeOfType<Api.BoundedContexts.GameManagement.Domain.Events.GameCreatedEvent>();
    }

    #endregion

    #region UpdateDetails Tests

    [Fact]
    public void UpdateDetails_WithNewTitle_UpdatesTitle()
    {
        // Arrange
        var game = CreateValidGame();
        var newTitle = new GameTitle("Updated Title");

        // Act
        game.UpdateDetails(title: newTitle);

        // Assert
        game.Title.Value.Should().Be("Updated Title");
    }

    [Fact]
    public void UpdateDetails_WithNewPublisher_UpdatesPublisher()
    {
        // Arrange
        var game = CreateValidGame();
        var newPublisher = new Publisher("Fantasy Flight Games");

        // Act
        game.UpdateDetails(publisher: newPublisher);

        // Assert
        game.Publisher!.Name.Should().Be("Fantasy Flight Games");
    }

    [Fact]
    public void UpdateDetails_WithNewYearPublished_UpdatesYear()
    {
        // Arrange
        var game = CreateValidGame();
        var newYear = new YearPublished(2020);

        // Act
        game.UpdateDetails(yearPublished: newYear);

        // Assert
        game.YearPublished!.Value.Should().Be(2020);
    }

    [Fact]
    public void UpdateDetails_WithNewPlayerCount_UpdatesPlayerCount()
    {
        // Arrange
        var game = CreateValidGame();
        var newPlayerCount = new PlayerCount(2, 6);

        // Act
        game.UpdateDetails(playerCount: newPlayerCount);

        // Assert
        game.PlayerCount!.Min.Should().Be(2);
        game.PlayerCount!.Max.Should().Be(6);
    }

    [Fact]
    public void UpdateDetails_WithNewPlayTime_UpdatesPlayTime()
    {
        // Arrange
        var game = CreateValidGame();
        var newPlayTime = new PlayTime(30, 60);

        // Act
        game.UpdateDetails(playTime: newPlayTime);

        // Assert
        game.PlayTime!.MinMinutes.Should().Be(30);
        game.PlayTime!.MaxMinutes.Should().Be(60);
    }

    [Fact]
    public void UpdateDetails_WithNullParameters_KeepsExistingValues()
    {
        // Arrange
        var publisher = new Publisher("Original Publisher");
        var game = new Game(Guid.NewGuid(), new GameTitle("Test"), publisher);

        // Act
        game.UpdateDetails(title: null, publisher: null);

        // Assert
        game.Publisher!.Name.Should().Be("Original Publisher");
    }

    [Fact]
    public void UpdateDetails_AddsDomainEvent()
    {
        // Arrange
        var game = CreateValidGame();
        game.ClearDomainEvents();

        // Act
        game.UpdateDetails(title: new GameTitle("New Title"));

        // Assert
        game.DomainEvents.Should().HaveCount(1);
        game.DomainEvents.First().Should().BeOfType<Api.BoundedContexts.GameManagement.Domain.Events.GameUpdatedEvent>();
    }

    #endregion

    #region SetImages Tests

    [Fact]
    public void SetImages_WithValidUrls_SetsImages()
    {
        // Arrange
        var game = CreateValidGame();
        var iconUrl = "https://example.com/icon.png";
        var imageUrl = "https://example.com/cover.jpg";

        // Act
        game.SetImages(iconUrl, imageUrl);

        // Assert
        game.IconUrl.Should().Be(iconUrl);
        game.ImageUrl.Should().Be(imageUrl);
    }

    [Fact]
    public void SetImages_WithNullUrls_SetsNull()
    {
        // Arrange
        var game = CreateValidGame();
        game.SetImages("https://example.com/old.png", "https://example.com/old.jpg");

        // Act
        game.SetImages(null, null);

        // Assert
        game.IconUrl.Should().BeNull();
        game.ImageUrl.Should().BeNull();
    }

    #endregion

    #region LinkToBgg Tests

    [Fact]
    public void LinkToBgg_WithValidId_SetsBggId()
    {
        // Arrange
        var game = CreateValidGame();

        // Act
        game.LinkToBgg(13);

        // Assert
        game.BggId.Should().Be(13);
        game.BggMetadata.Should().BeNull();
    }

    [Fact]
    public void LinkToBgg_WithMetadata_SetsBggIdAndMetadata()
    {
        // Arrange
        var game = CreateValidGame();
        var metadata = "{\"thumbnail\": \"url\"}";

        // Act
        game.LinkToBgg(13, metadata);

        // Assert
        game.BggId.Should().Be(13);
        game.BggMetadata.Should().Be(metadata);
    }

    [Fact]
    public void LinkToBgg_WithZeroId_ThrowsArgumentException()
    {
        // Arrange
        var game = CreateValidGame();

        // Act
        var action = () => game.LinkToBgg(0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*BGG ID must be positive*")
            .WithParameterName("bggId");
    }

    [Fact]
    public void LinkToBgg_WithNegativeId_ThrowsArgumentException()
    {
        // Arrange
        var game = CreateValidGame();

        // Act
        var action = () => game.LinkToBgg(-1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*BGG ID must be positive*");
    }

    [Fact]
    public void LinkToBgg_AddsDomainEvent()
    {
        // Arrange
        var game = CreateValidGame();
        game.ClearDomainEvents();

        // Act
        game.LinkToBgg(13);

        // Assert
        game.DomainEvents.Should().HaveCount(1);
        game.DomainEvents.First().Should().BeOfType<Api.BoundedContexts.GameManagement.Domain.Events.GameLinkedToBggEvent>();
    }

    #endregion

    #region LinkToSharedGame Tests

    [Fact]
    public void LinkToSharedGame_WithValidId_SetsSharedGameId()
    {
        // Arrange
        var game = CreateValidGame();
        var sharedGameId = Guid.NewGuid();

        // Act
        game.LinkToSharedGame(sharedGameId);

        // Assert
        game.SharedGameId.Should().Be(sharedGameId);
    }

    [Fact]
    public void LinkToSharedGame_WithEmptyId_ThrowsArgumentException()
    {
        // Arrange
        var game = CreateValidGame();

        // Act
        var action = () => game.LinkToSharedGame(Guid.Empty);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*SharedGameId cannot be empty*")
            .WithParameterName("sharedGameId");
    }

    #endregion

    #region SupportsPlayerCount Tests

    [Fact]
    public void SupportsPlayerCount_WithMatchingCount_ReturnsTrue()
    {
        // Arrange
        var game = new Game(Guid.NewGuid(), new GameTitle("Test"),
            playerCount: new PlayerCount(2, 4));

        // Act & Assert
        game.SupportsPlayerCount(2).Should().BeTrue();
        game.SupportsPlayerCount(3).Should().BeTrue();
        game.SupportsPlayerCount(4).Should().BeTrue();
    }

    [Fact]
    public void SupportsPlayerCount_WithOutOfRangeCount_ReturnsFalse()
    {
        // Arrange
        var game = new Game(Guid.NewGuid(), new GameTitle("Test"),
            playerCount: new PlayerCount(2, 4));

        // Act & Assert
        game.SupportsPlayerCount(1).Should().BeFalse();
        game.SupportsPlayerCount(5).Should().BeFalse();
    }

    [Fact]
    public void SupportsPlayerCount_WithNullPlayerCount_ReturnsTrue()
    {
        // Arrange
        var game = CreateValidGame();

        // Act & Assert
        game.SupportsPlayerCount(1).Should().BeTrue();
        game.SupportsPlayerCount(100).Should().BeTrue();
    }

    #endregion

    #region SupportsSolo Tests

    [Fact]
    public void SupportsSolo_WithSoloCapableGame_ReturnsTrue()
    {
        // Arrange
        var game = new Game(Guid.NewGuid(), new GameTitle("Solo Game"),
            playerCount: new PlayerCount(1, 4));

        // Act & Assert
        game.SupportsSolo.Should().BeTrue();
    }

    [Fact]
    public void SupportsSolo_WithMultiplayerOnlyGame_ReturnsFalse()
    {
        // Arrange
        var game = new Game(Guid.NewGuid(), new GameTitle("Multiplayer Only"),
            playerCount: new PlayerCount(2, 4));

        // Act & Assert
        game.SupportsSolo.Should().BeFalse();
    }

    [Fact]
    public void SupportsSolo_WithNullPlayerCount_ReturnsFalse()
    {
        // Arrange
        var game = CreateValidGame();

        // Act & Assert
        game.SupportsSolo.Should().BeFalse();
    }

    #endregion

    #region IsPublished Invariant Tests (Spec-panel C-1)

    [Fact]
    public void IsPublished_WhenApprovalStatusApprovedAndPublishedAtSet_ReturnsTrue()
    {
        // Arrange
        var game = new Game(Guid.NewGuid(), new GameTitle("Catan"));
        game.LinkToSharedGame(Guid.NewGuid());

        // Act
        game.Publish(ApprovalStatus.Approved);

        // Assert
        game.IsPublished.Should().BeTrue("game approved with PublishedAt set must be published");
        game.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        game.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public void IsPublished_WhenApprovalStatusRejected_ReturnsFalse()
    {
        // Arrange
        var game = new Game(Guid.NewGuid(), new GameTitle("Catan"));
        game.LinkToSharedGame(Guid.NewGuid());
        game.Publish(ApprovalStatus.Approved);

        // Act
        game.Publish(ApprovalStatus.Rejected);

        // Assert
        game.IsPublished.Should().BeFalse("rejected game must not be published");
        game.PublishedAt.Should().BeNull("PublishedAt is cleared on rejection");
    }

    [Fact]
    public void IsPublished_WhenDraft_ReturnsFalse()
    {
        // Arrange & Act
        var game = new Game(Guid.NewGuid(), new GameTitle("Catan"));

        // Assert
        game.IsPublished.Should().BeFalse("new game defaults to Draft and is not published");
        game.ApprovalStatus.Should().Be(ApprovalStatus.Draft);
    }

    [Fact]
    public void IsPublished_CannotBeTrueWithoutSharedGameLink()
    {
        // Arrange
        var game = new Game(Guid.NewGuid(), new GameTitle("Catan"));

        // Act
        var action = () => game.Publish(ApprovalStatus.Approved);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*SharedGameCatalog*");
    }

    #endregion

    #region UnlinkFromSharedGame Tests (Spec-panel M-3)

    [Fact]
    public void UnlinkFromSharedGame_WhenLinked_RemovesSharedGameId()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var game = new Game(Guid.NewGuid(), new GameTitle("Catan"));
        game.LinkToSharedGame(sharedGameId);
        game.SharedGameId.Should().Be(sharedGameId);

        // Act
        game.UnlinkFromSharedGame();

        // Assert
        game.SharedGameId.Should().BeNull("unlinking clears the SharedGameId");
    }

    [Fact]
    public void UnlinkFromSharedGame_WhenLinked_RaisesGameUnlinkedEvent()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var game = new Game(Guid.NewGuid(), new GameTitle("Catan"));
        game.LinkToSharedGame(sharedGameId);
        game.ClearDomainEvents();

        // Act
        game.UnlinkFromSharedGame();

        // Assert
        game.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Api.BoundedContexts.GameManagement.Domain.Events.GameUnlinkedFromSharedCatalogEvent>();

        var evt = (Api.BoundedContexts.GameManagement.Domain.Events.GameUnlinkedFromSharedCatalogEvent)game.DomainEvents.Single();
        evt.GameId.Should().Be(game.Id);
        evt.PreviousSharedGameId.Should().Be(sharedGameId);
    }

    [Fact]
    public void UnlinkFromSharedGame_WhenAlreadyUnlinked_IsIdempotent()
    {
        // Arrange
        var game = new Game(Guid.NewGuid(), new GameTitle("Catan"));

        // Act
        var action = () => game.UnlinkFromSharedGame();

        // Assert
        action.Should().NotThrow("unlinking a non-linked game is a safe no-op");
        game.SharedGameId.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private static Game CreateValidGame()
    {
        return new Game(Guid.NewGuid(), new GameTitle("Test Game"));
    }

    #endregion
}
