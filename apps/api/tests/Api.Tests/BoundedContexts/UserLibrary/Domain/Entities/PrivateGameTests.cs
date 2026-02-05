using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// Tests for the PrivateGame aggregate root.
/// Issue #3662: Phase 1 - Data Model & Core Infrastructure for Private Games.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserLibrary")]
public sealed class PrivateGameTests
{
    private static readonly Guid ValidOwnerId = Guid.NewGuid();
    private const int ValidBggId = 12345;
    private const string ValidTitle = "Test Game";
    private const int ValidMinPlayers = 2;
    private const int ValidMaxPlayers = 4;

    #region CreateFromBgg Tests

    [Fact]
    public void CreateFromBgg_WithValidParameters_CreatesGame()
    {
        // Arrange & Act
        var game = PrivateGame.CreateFromBgg(
            ownerId: ValidOwnerId,
            bggId: ValidBggId,
            title: ValidTitle,
            yearPublished: 2020,
            description: "A great game",
            minPlayers: ValidMinPlayers,
            maxPlayers: ValidMaxPlayers,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg");

        // Assert
        game.Id.Should().NotBe(Guid.Empty);
        game.OwnerId.Should().Be(ValidOwnerId);
        game.BggId.Should().Be(ValidBggId);
        game.Title.Should().Be(ValidTitle);
        game.YearPublished.Should().Be(2020);
        game.Description.Should().Be("A great game");
        game.MinPlayers.Should().Be(ValidMinPlayers);
        game.MaxPlayers.Should().Be(ValidMaxPlayers);
        game.PlayingTimeMinutes.Should().Be(60);
        game.MinAge.Should().Be(10);
        game.ComplexityRating.Should().Be(2.5m);
        game.ImageUrl.Should().Be("https://example.com/image.jpg");
        game.ThumbnailUrl.Should().Be("https://example.com/thumb.jpg");
        game.Source.Should().Be(PrivateGameSource.BoardGameGeek);
        game.BggSyncedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        game.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        game.IsDeleted.Should().BeFalse();
        game.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void CreateFromBgg_WithEmptyOwnerId_ThrowsArgumentException()
    {
        // Act
        var action = () => PrivateGame.CreateFromBgg(
            ownerId: Guid.Empty,
            bggId: ValidBggId,
            title: ValidTitle,
            yearPublished: 2020,
            description: null,
            minPlayers: ValidMinPlayers,
            maxPlayers: ValidMaxPlayers,
            playingTimeMinutes: null,
            minAge: null,
            complexityRating: null,
            imageUrl: null,
            thumbnailUrl: null);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("ownerId")
            .WithMessage("*OwnerId cannot be empty*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void CreateFromBgg_WithInvalidBggId_ThrowsArgumentException(int invalidBggId)
    {
        // Act
        var action = () => PrivateGame.CreateFromBgg(
            ownerId: ValidOwnerId,
            bggId: invalidBggId,
            title: ValidTitle,
            yearPublished: null,
            description: null,
            minPlayers: ValidMinPlayers,
            maxPlayers: ValidMaxPlayers,
            playingTimeMinutes: null,
            minAge: null,
            complexityRating: null,
            imageUrl: null,
            thumbnailUrl: null);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("bggId")
            .WithMessage("*BggId must be a positive integer*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateFromBgg_WithInvalidTitle_ThrowsArgumentException(string? invalidTitle)
    {
        // Act
        var action = () => PrivateGame.CreateFromBgg(
            ownerId: ValidOwnerId,
            bggId: ValidBggId,
            title: invalidTitle!,
            yearPublished: null,
            description: null,
            minPlayers: ValidMinPlayers,
            maxPlayers: ValidMaxPlayers,
            playingTimeMinutes: null,
            minAge: null,
            complexityRating: null,
            imageUrl: null,
            thumbnailUrl: null);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("title")
            .WithMessage("*Title is required*");
    }

    [Fact]
    public void CreateFromBgg_WithTitleTooLong_ThrowsArgumentException()
    {
        // Arrange
        var longTitle = new string('A', 201);

        // Act
        var action = () => PrivateGame.CreateFromBgg(
            ownerId: ValidOwnerId,
            bggId: ValidBggId,
            title: longTitle,
            yearPublished: null,
            description: null,
            minPlayers: ValidMinPlayers,
            maxPlayers: ValidMaxPlayers,
            playingTimeMinutes: null,
            minAge: null,
            complexityRating: null,
            imageUrl: null,
            thumbnailUrl: null);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("title")
            .WithMessage("*Title cannot exceed 200 characters*");
    }

    #endregion

    #region CreateManual Tests

    [Fact]
    public void CreateManual_WithValidParameters_CreatesGame()
    {
        // Arrange & Act
        var game = PrivateGame.CreateManual(
            ownerId: ValidOwnerId,
            title: ValidTitle,
            minPlayers: ValidMinPlayers,
            maxPlayers: ValidMaxPlayers,
            yearPublished: 2022,
            description: "My custom game",
            playingTimeMinutes: 45,
            minAge: 8,
            complexityRating: 1.5m,
            imageUrl: "https://example.com/custom.jpg");

        // Assert
        game.Id.Should().NotBe(Guid.Empty);
        game.OwnerId.Should().Be(ValidOwnerId);
        game.BggId.Should().BeNull();
        game.Title.Should().Be(ValidTitle);
        game.YearPublished.Should().Be(2022);
        game.Description.Should().Be("My custom game");
        game.MinPlayers.Should().Be(ValidMinPlayers);
        game.MaxPlayers.Should().Be(ValidMaxPlayers);
        game.PlayingTimeMinutes.Should().Be(45);
        game.MinAge.Should().Be(8);
        game.ComplexityRating.Should().Be(1.5m);
        game.ImageUrl.Should().Be("https://example.com/custom.jpg");
        game.ThumbnailUrl.Should().BeNull();
        game.Source.Should().Be(PrivateGameSource.Manual);
        game.BggSyncedAt.Should().BeNull();
        game.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        game.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void CreateManual_WithMinimalParameters_CreatesGame()
    {
        // Arrange & Act
        var game = PrivateGame.CreateManual(
            ownerId: ValidOwnerId,
            title: ValidTitle,
            minPlayers: ValidMinPlayers,
            maxPlayers: ValidMaxPlayers);

        // Assert
        game.OwnerId.Should().Be(ValidOwnerId);
        game.Title.Should().Be(ValidTitle);
        game.MinPlayers.Should().Be(ValidMinPlayers);
        game.MaxPlayers.Should().Be(ValidMaxPlayers);
        game.Source.Should().Be(PrivateGameSource.Manual);
        game.YearPublished.Should().BeNull();
        game.Description.Should().BeNull();
        game.PlayingTimeMinutes.Should().BeNull();
        game.MinAge.Should().BeNull();
        game.ComplexityRating.Should().BeNull();
        game.ImageUrl.Should().BeNull();
    }

    [Fact]
    public void CreateManual_WithEmptyOwnerId_ThrowsArgumentException()
    {
        // Act
        var action = () => PrivateGame.CreateManual(
            ownerId: Guid.Empty,
            title: ValidTitle,
            minPlayers: ValidMinPlayers,
            maxPlayers: ValidMaxPlayers);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("ownerId")
            .WithMessage("*OwnerId cannot be empty*");
    }

    #endregion

    #region Player Validation Tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateManual_WithInvalidMinPlayers_ThrowsArgumentException(int invalidMinPlayers)
    {
        // Act
        var action = () => PrivateGame.CreateManual(
            ownerId: ValidOwnerId,
            title: ValidTitle,
            minPlayers: invalidMinPlayers,
            maxPlayers: ValidMaxPlayers);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("minPlayers")
            .WithMessage("*MinPlayers must be at least 1*");
    }

    [Fact]
    public void CreateManual_WithMinPlayersExceeding100_ThrowsArgumentException()
    {
        // Act
        var action = () => PrivateGame.CreateManual(
            ownerId: ValidOwnerId,
            title: ValidTitle,
            minPlayers: 101,
            maxPlayers: 101);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("minPlayers")
            .WithMessage("*MinPlayers cannot exceed 100*");
    }

    [Fact]
    public void CreateManual_WithMaxPlayersLessThanMinPlayers_ThrowsArgumentException()
    {
        // Act
        var action = () => PrivateGame.CreateManual(
            ownerId: ValidOwnerId,
            title: ValidTitle,
            minPlayers: 4,
            maxPlayers: 2);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("maxPlayers")
            .WithMessage("*MaxPlayers must be greater than or equal to MinPlayers*");
    }

    [Fact]
    public void CreateManual_WithMaxPlayersExceeding100_ThrowsArgumentException()
    {
        // Act
        var action = () => PrivateGame.CreateManual(
            ownerId: ValidOwnerId,
            title: ValidTitle,
            minPlayers: 1,
            maxPlayers: 101);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("maxPlayers")
            .WithMessage("*MaxPlayers cannot exceed 100*");
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 4)]
    [InlineData(1, 100)]
    [InlineData(100, 100)]
    public void CreateManual_WithValidPlayerRange_CreatesGame(int minPlayers, int maxPlayers)
    {
        // Act
        var game = PrivateGame.CreateManual(
            ownerId: ValidOwnerId,
            title: ValidTitle,
            minPlayers: minPlayers,
            maxPlayers: maxPlayers);

        // Assert
        game.MinPlayers.Should().Be(minPlayers);
        game.MaxPlayers.Should().Be(maxPlayers);
    }

    #endregion

    #region Complexity Rating Validation Tests

    [Theory]
    [InlineData(0.9)]
    [InlineData(5.1)]
    [InlineData(0)]
    [InlineData(10)]
    public void CreateManual_WithInvalidComplexityRating_ThrowsArgumentException(decimal invalidRating)
    {
        // Act
        var action = () => PrivateGame.CreateManual(
            ownerId: ValidOwnerId,
            title: ValidTitle,
            minPlayers: ValidMinPlayers,
            maxPlayers: ValidMaxPlayers,
            complexityRating: invalidRating);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("rating")
            .WithMessage("*ComplexityRating must be between 1.0 and 5.0*");
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.5)]
    [InlineData(3.0)]
    [InlineData(5.0)]
    public void CreateManual_WithValidComplexityRating_CreatesGame(decimal validRating)
    {
        // Act
        var game = PrivateGame.CreateManual(
            ownerId: ValidOwnerId,
            title: ValidTitle,
            minPlayers: ValidMinPlayers,
            maxPlayers: ValidMaxPlayers,
            complexityRating: validRating);

        // Assert
        game.ComplexityRating.Should().Be(validRating);
    }

    #endregion

    #region UpdateInfo Tests

    [Fact]
    public void UpdateInfo_WithValidParameters_UpdatesAllFields()
    {
        // Arrange
        var game = CreateValidManualGame();
        var originalCreatedAt = game.CreatedAt;

        // Act
        game.UpdateInfo(
            title: "Updated Title",
            minPlayers: 3,
            maxPlayers: 6,
            yearPublished: 2023,
            description: "Updated description",
            playingTimeMinutes: 90,
            minAge: 12,
            complexityRating: 3.5m,
            imageUrl: "https://example.com/new.jpg");

        // Assert
        game.Title.Should().Be("Updated Title");
        game.MinPlayers.Should().Be(3);
        game.MaxPlayers.Should().Be(6);
        game.YearPublished.Should().Be(2023);
        game.Description.Should().Be("Updated description");
        game.PlayingTimeMinutes.Should().Be(90);
        game.MinAge.Should().Be(12);
        game.ComplexityRating.Should().Be(3.5m);
        game.ImageUrl.Should().Be("https://example.com/new.jpg");
        game.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        game.CreatedAt.Should().Be(originalCreatedAt);
    }

    [Fact]
    public void UpdateInfo_WithInvalidTitle_ThrowsArgumentException()
    {
        // Arrange
        var game = CreateValidManualGame();

        // Act
        var action = () => game.UpdateInfo(
            title: "",
            minPlayers: ValidMinPlayers,
            maxPlayers: ValidMaxPlayers,
            yearPublished: null,
            description: null,
            playingTimeMinutes: null,
            minAge: null,
            complexityRating: null,
            imageUrl: null);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("title");
    }

    [Fact]
    public void UpdateInfo_WithInvalidPlayers_ThrowsArgumentException()
    {
        // Arrange
        var game = CreateValidManualGame();

        // Act
        var action = () => game.UpdateInfo(
            title: ValidTitle,
            minPlayers: 5,
            maxPlayers: 3,
            yearPublished: null,
            description: null,
            playingTimeMinutes: null,
            minAge: null,
            complexityRating: null,
            imageUrl: null);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("maxPlayers");
    }

    #endregion

    #region SyncFromBgg Tests

    [Fact]
    public void SyncFromBgg_ForBggSourcedGame_UpdatesAllFields()
    {
        // Arrange
        var game = CreateValidBggGame();
        var originalBggId = game.BggId;

        // Act
        game.SyncFromBgg(
            title: "Synced Title",
            yearPublished: 2024,
            description: "Synced description",
            minPlayers: 1,
            maxPlayers: 8,
            playingTimeMinutes: 120,
            minAge: 14,
            complexityRating: 4.0m,
            imageUrl: "https://example.com/synced.jpg",
            thumbnailUrl: "https://example.com/synced_thumb.jpg");

        // Assert
        game.Title.Should().Be("Synced Title");
        game.YearPublished.Should().Be(2024);
        game.Description.Should().Be("Synced description");
        game.MinPlayers.Should().Be(1);
        game.MaxPlayers.Should().Be(8);
        game.PlayingTimeMinutes.Should().Be(120);
        game.MinAge.Should().Be(14);
        game.ComplexityRating.Should().Be(4.0m);
        game.ImageUrl.Should().Be("https://example.com/synced.jpg");
        game.ThumbnailUrl.Should().Be("https://example.com/synced_thumb.jpg");
        game.BggSyncedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        game.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        game.BggId.Should().Be(originalBggId);
    }

    [Fact]
    public void SyncFromBgg_ForManualSourcedGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateValidManualGame();

        // Act
        var action = () => game.SyncFromBgg(
            title: "Synced Title",
            yearPublished: null,
            description: null,
            minPlayers: ValidMinPlayers,
            maxPlayers: ValidMaxPlayers,
            playingTimeMinutes: null,
            minAge: null,
            complexityRating: null,
            imageUrl: null,
            thumbnailUrl: null);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot sync BGG data for a manually created game");
    }

    #endregion

    #region Delete and Restore Tests

    [Fact]
    public void Delete_WhenNotDeleted_SoftDeletesGame()
    {
        // Arrange
        var game = CreateValidManualGame();

        // Act
        game.Delete();

        // Assert
        game.IsDeleted.Should().BeTrue();
        game.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Delete_WhenAlreadyDeleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateValidManualGame();
        game.Delete();

        // Act
        var action = () => game.Delete();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Game is already deleted");
    }

    [Fact]
    public void Restore_WhenDeleted_RestoresGame()
    {
        // Arrange
        var game = CreateValidManualGame();
        game.Delete();

        // Act
        game.Restore();

        // Assert
        game.IsDeleted.Should().BeFalse();
        game.DeletedAt.Should().BeNull();
        game.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Restore_WhenNotDeleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateValidManualGame();

        // Act
        var action = () => game.Restore();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Game is not deleted");
    }

    #endregion

    #region ToSharedGame Tests

    [Fact]
    public void ToSharedGame_ReturnsCorrectPromotionData()
    {
        // Arrange
        var game = PrivateGame.CreateFromBgg(
            ownerId: ValidOwnerId,
            bggId: ValidBggId,
            title: ValidTitle,
            yearPublished: 2020,
            description: "Test description",
            minPlayers: ValidMinPlayers,
            maxPlayers: ValidMaxPlayers,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg");

        // Act
        var promotionData = game.ToSharedGame();

        // Assert
        promotionData.BggId.Should().Be(ValidBggId);
        promotionData.Title.Should().Be(ValidTitle);
        promotionData.YearPublished.Should().Be(2020);
        promotionData.Description.Should().Be("Test description");
        promotionData.MinPlayers.Should().Be(ValidMinPlayers);
        promotionData.MaxPlayers.Should().Be(ValidMaxPlayers);
        promotionData.PlayingTimeMinutes.Should().Be(60);
        promotionData.MinAge.Should().Be(10);
        promotionData.ComplexityRating.Should().Be(2.5m);
        promotionData.ImageUrl.Should().Be("https://example.com/image.jpg");
        promotionData.ThumbnailUrl.Should().Be("https://example.com/thumb.jpg");
        promotionData.OriginalOwnerId.Should().Be(ValidOwnerId);
        promotionData.PrivateGameId.Should().Be(game.Id);
    }

    [Fact]
    public void ToSharedGame_WithNullOptionalFields_ReturnsDefaultValues()
    {
        // Arrange
        var game = PrivateGame.CreateManual(
            ownerId: ValidOwnerId,
            title: ValidTitle,
            minPlayers: ValidMinPlayers,
            maxPlayers: ValidMaxPlayers);

        // Act
        var promotionData = game.ToSharedGame();

        // Assert
        promotionData.BggId.Should().BeNull();
        promotionData.YearPublished.Should().Be(DateTime.UtcNow.Year);
        promotionData.Description.Should().BeEmpty();
        promotionData.PlayingTimeMinutes.Should().Be(60);
        promotionData.MinAge.Should().Be(0);
        promotionData.ComplexityRating.Should().BeNull();
        promotionData.ImageUrl.Should().BeEmpty();
        promotionData.ThumbnailUrl.Should().BeEmpty();
    }

    [Fact]
    public void ToSharedGame_WithImageButNoThumbnail_UseImageForThumbnail()
    {
        // Arrange
        var game = PrivateGame.CreateManual(
            ownerId: ValidOwnerId,
            title: ValidTitle,
            minPlayers: ValidMinPlayers,
            maxPlayers: ValidMaxPlayers,
            imageUrl: "https://example.com/image.jpg");

        // Act
        var promotionData = game.ToSharedGame();

        // Assert
        promotionData.ImageUrl.Should().Be("https://example.com/image.jpg");
        promotionData.ThumbnailUrl.Should().Be("https://example.com/image.jpg");
    }

    #endregion

    #region Helper Methods

    private static PrivateGame CreateValidManualGame()
    {
        return PrivateGame.CreateManual(
            ownerId: ValidOwnerId,
            title: ValidTitle,
            minPlayers: ValidMinPlayers,
            maxPlayers: ValidMaxPlayers);
    }

    private static PrivateGame CreateValidBggGame()
    {
        return PrivateGame.CreateFromBgg(
            ownerId: ValidOwnerId,
            bggId: ValidBggId,
            title: ValidTitle,
            yearPublished: 2020,
            description: "A test game",
            minPlayers: ValidMinPlayers,
            maxPlayers: ValidMaxPlayers,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg");
    }

    #endregion
}
