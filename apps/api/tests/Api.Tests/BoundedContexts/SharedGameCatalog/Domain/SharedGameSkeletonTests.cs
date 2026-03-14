using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class SharedGameSkeletonTests
{
    private static readonly TimeProvider TimeProvider = TimeProvider.System;
    private static readonly Guid AdminUserId = Guid.NewGuid();

    #region CreateSkeleton

    [Fact]
    public void CreateSkeleton_WithValidTitle_ShouldCreateWithDefaults()
    {
        var game = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider);

        Assert.Equal("Catan", game.Title);
        Assert.Equal(GameDataStatus.Skeleton, game.GameDataStatus);
        Assert.Equal(GameStatus.Draft, game.Status);
        Assert.Equal(0, game.YearPublished);
        Assert.Equal(0, game.MinPlayers);
        Assert.Equal(0, game.MaxPlayers);
        Assert.Equal(0, game.PlayingTimeMinutes);
        Assert.Equal(0, game.MinAge);
        Assert.Null(game.ComplexityRating);
        Assert.Null(game.AverageRating);
        Assert.Equal(string.Empty, game.ImageUrl);
        Assert.Equal(string.Empty, game.ThumbnailUrl);
        Assert.Null(game.Rules);
        Assert.False(game.IsDeleted);
        Assert.False(game.HasUploadedPdf);
        Assert.NotEqual(Guid.Empty, game.Id);
    }

    [Fact]
    public void CreateSkeleton_WithBggId_ShouldSetBggId()
    {
        var game = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider, bggId: 13);

        Assert.Equal(13, game.BggId);
    }

    [Fact]
    public void CreateSkeleton_WithEmptyTitle_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            SharedGame.CreateSkeleton("", AdminUserId, TimeProvider));
    }

    [Fact]
    public void CreateSkeleton_WithWhitespaceTitle_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            SharedGame.CreateSkeleton("   ", AdminUserId, TimeProvider));
    }

    [Fact]
    public void CreateSkeleton_WithTitleOver500Chars_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            SharedGame.CreateSkeleton(new string('x', 501), AdminUserId, TimeProvider));
    }

    [Fact]
    public void CreateSkeleton_WithEmptyCreatedBy_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            SharedGame.CreateSkeleton("Catan", Guid.Empty, TimeProvider));
    }

    [Fact]
    public void CreateSkeleton_ShouldTrimTitle()
    {
        var game = SharedGame.CreateSkeleton("  Catan  ", AdminUserId, TimeProvider);

        Assert.Equal("Catan", game.Title);
    }

    [Fact]
    public void CreateSkeleton_ShouldSetTimestamps()
    {
        var before = DateTime.UtcNow;
        var game = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider);
        var after = DateTime.UtcNow;

        Assert.InRange(game.CreatedAt, before, after);
    }

    #endregion

    #region State Machine - TransitionDataStatusTo

    [Theory]
    [InlineData(GameDataStatus.Skeleton, GameDataStatus.EnrichmentQueued)]
    [InlineData(GameDataStatus.EnrichmentQueued, GameDataStatus.Enriching)]
    [InlineData(GameDataStatus.Enriching, GameDataStatus.Enriched)]
    [InlineData(GameDataStatus.Enriching, GameDataStatus.Failed)]
    [InlineData(GameDataStatus.Enriched, GameDataStatus.PdfDownloading)]
    [InlineData(GameDataStatus.Enriched, GameDataStatus.Complete)]
    [InlineData(GameDataStatus.PdfDownloading, GameDataStatus.Complete)]
    [InlineData(GameDataStatus.PdfDownloading, GameDataStatus.Enriched)]
    [InlineData(GameDataStatus.Failed, GameDataStatus.EnrichmentQueued)]
    public void TransitionDataStatusTo_ValidTransition_ShouldSucceed(
        GameDataStatus from, GameDataStatus to)
    {
        var game = CreateGameInStatus(from);

        game.TransitionDataStatusTo(to);

        Assert.Equal(to, game.GameDataStatus);
    }

    [Theory]
    [InlineData(GameDataStatus.Skeleton, GameDataStatus.Complete)]
    [InlineData(GameDataStatus.Skeleton, GameDataStatus.Enriched)]
    [InlineData(GameDataStatus.Skeleton, GameDataStatus.Enriching)]
    [InlineData(GameDataStatus.EnrichmentQueued, GameDataStatus.Complete)]
    [InlineData(GameDataStatus.Enriched, GameDataStatus.Skeleton)]
    [InlineData(GameDataStatus.Complete, GameDataStatus.EnrichmentQueued)]
    [InlineData(GameDataStatus.Complete, GameDataStatus.Skeleton)]
    [InlineData(GameDataStatus.Failed, GameDataStatus.Complete)]
    [InlineData(GameDataStatus.Failed, GameDataStatus.Enriched)]
    public void TransitionDataStatusTo_InvalidTransition_ShouldThrow(
        GameDataStatus from, GameDataStatus to)
    {
        var game = CreateGameInStatus(from);

        Assert.Throws<InvalidOperationException>(() =>
            game.TransitionDataStatusTo(to));
    }

    #endregion

    #region EnrichFromBgg

    [Fact]
    public void EnrichFromBgg_InEnrichingState_ShouldUpdateAllFields()
    {
        var game = CreateGameInStatus(GameDataStatus.Enriching);

        game.EnrichFromBgg(
            description: "Trade, build, settle",
            yearPublished: 1995,
            minPlayers: 3, maxPlayers: 4,
            playingTimeMinutes: 90, minAge: 10,
            complexityRating: 2.3m, averageRating: 7.2m,
            imageUrl: "https://cf.geekdo-images.com/catan.jpg",
            thumbnailUrl: "https://cf.geekdo-images.com/catan_t.jpg",
            rulebookUrl: "https://example.com/catan-rules.pdf");

        Assert.Equal(GameDataStatus.Enriched, game.GameDataStatus);
        Assert.Equal(1995, game.YearPublished);
        Assert.Equal("Trade, build, settle", game.Description);
        Assert.Equal(3, game.MinPlayers);
        Assert.Equal(4, game.MaxPlayers);
        Assert.Equal(90, game.PlayingTimeMinutes);
        Assert.Equal(10, game.MinAge);
        Assert.Equal(2.3m, game.ComplexityRating);
        Assert.Equal(7.2m, game.AverageRating);
        Assert.Equal("https://cf.geekdo-images.com/catan.jpg", game.ImageUrl);
        Assert.Equal("https://cf.geekdo-images.com/catan_t.jpg", game.ThumbnailUrl);
        Assert.NotNull(game.Rules);
        Assert.Equal("https://example.com/catan-rules.pdf", game.Rules!.ExternalUrl);
    }

    [Fact]
    public void EnrichFromBgg_WithNullRulebookUrl_ShouldNotSetRules()
    {
        var game = CreateGameInStatus(GameDataStatus.Enriching);

        game.EnrichFromBgg(
            description: "A game",
            yearPublished: 2020,
            minPlayers: 2, maxPlayers: 5,
            playingTimeMinutes: 60, minAge: 8,
            complexityRating: null, averageRating: null,
            imageUrl: "https://example.com/img.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rulebookUrl: null);

        Assert.Null(game.Rules);
    }

    [Fact]
    public void EnrichFromBgg_WithBggId_ShouldUpdateBggId()
    {
        var game = CreateGameInStatus(GameDataStatus.Enriching);

        game.EnrichFromBgg(
            description: "A game",
            yearPublished: 2020,
            minPlayers: 2, maxPlayers: 5,
            playingTimeMinutes: 60, minAge: 8,
            complexityRating: null, averageRating: null,
            imageUrl: "https://example.com/img.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rulebookUrl: null,
            bggId: 42);

        Assert.Equal(42, game.BggId);
    }

    [Fact]
    public void EnrichFromBgg_NotInEnrichingState_ShouldThrow()
    {
        var game = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider);

        Assert.Throws<InvalidOperationException>(() =>
            game.EnrichFromBgg(
                description: "desc", yearPublished: 2020,
                minPlayers: 2, maxPlayers: 4,
                playingTimeMinutes: 60, minAge: 8,
                complexityRating: null, averageRating: null,
                imageUrl: "https://example.com/img.jpg",
                thumbnailUrl: "https://example.com/thumb.jpg",
                rulebookUrl: null));
    }

    [Fact]
    public void EnrichFromBgg_WithInvalidData_ShouldThrowValidation()
    {
        var game = CreateGameInStatus(GameDataStatus.Enriching);

        // minPlayers = 0 should fail validation
        Assert.Throws<ArgumentException>(() =>
            game.EnrichFromBgg(
                description: "desc", yearPublished: 2020,
                minPlayers: 0, maxPlayers: 4,
                playingTimeMinutes: 60, minAge: 8,
                complexityRating: null, averageRating: null,
                imageUrl: "https://example.com/img.jpg",
                thumbnailUrl: "https://example.com/thumb.jpg",
                rulebookUrl: null));
    }

    #endregion

    #region MarkDataComplete

    [Fact]
    public void MarkDataComplete_FromEnriched_ShouldTransition()
    {
        var game = CreateGameInStatus(GameDataStatus.Enriched);

        game.MarkDataComplete();

        Assert.Equal(GameDataStatus.Complete, game.GameDataStatus);
    }

    [Fact]
    public void MarkDataComplete_FromSkeleton_ShouldThrow()
    {
        var game = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider);

        Assert.Throws<InvalidOperationException>(() => game.MarkDataComplete());
    }

    #endregion

    #region SubmitForApproval with GameDataStatus guard

    [Fact]
    public void SubmitForApproval_SkeletonGame_ShouldThrow()
    {
        var game = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider);

        Assert.Throws<InvalidOperationException>(() =>
            game.SubmitForApproval(AdminUserId));
    }

    [Fact]
    public void SubmitForApproval_EnrichedGame_ShouldSucceed()
    {
        var game = CreateGameInStatus(GameDataStatus.Enriched);

        game.SubmitForApproval(AdminUserId);

        Assert.Equal(GameStatus.PendingApproval, game.Status);
    }

    #endregion

    #region SetHasUploadedPdf

    [Fact]
    public void SetHasUploadedPdf_ShouldSetFlagToTrue()
    {
        var game = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider);

        game.SetHasUploadedPdf();

        Assert.True(game.HasUploadedPdf);
    }

    #endregion

    #region Failed → Re-enqueue

    [Fact]
    public void Failed_CanBeReenqueued()
    {
        var game = CreateGameInStatus(GameDataStatus.Failed);

        game.TransitionDataStatusTo(GameDataStatus.EnrichmentQueued);

        Assert.Equal(GameDataStatus.EnrichmentQueued, game.GameDataStatus);
    }

    #endregion

    #region Helpers

    private static SharedGame CreateGameInStatus(GameDataStatus targetStatus)
    {
        var game = SharedGame.CreateSkeleton("Test Game", AdminUserId, TimeProvider);

        // Walk through valid transitions to reach the target status
        var path = GetPathToStatus(targetStatus);
        foreach (var step in path)
        {
            if (step == GameDataStatus.Enriched)
            {
                // Need to go through EnrichFromBgg for Enriching → Enriched
                game.EnrichFromBgg(
                    description: "Test description",
                    yearPublished: 2020,
                    minPlayers: 2, maxPlayers: 4,
                    playingTimeMinutes: 60, minAge: 8,
                    complexityRating: 2.5m, averageRating: 7.0m,
                    imageUrl: "https://example.com/img.jpg",
                    thumbnailUrl: "https://example.com/thumb.jpg",
                    rulebookUrl: null);
            }
            else
            {
                game.TransitionDataStatusTo(step);
            }
        }

        return game;
    }

    private static List<GameDataStatus> GetPathToStatus(GameDataStatus target)
    {
        return target switch
        {
            GameDataStatus.Skeleton => [],
            GameDataStatus.EnrichmentQueued => [GameDataStatus.EnrichmentQueued],
            GameDataStatus.Enriching => [GameDataStatus.EnrichmentQueued, GameDataStatus.Enriching],
            GameDataStatus.Enriched => [GameDataStatus.EnrichmentQueued, GameDataStatus.Enriching, GameDataStatus.Enriched],
            GameDataStatus.PdfDownloading => [GameDataStatus.EnrichmentQueued, GameDataStatus.Enriching, GameDataStatus.Enriched, GameDataStatus.PdfDownloading],
            GameDataStatus.Complete => [GameDataStatus.EnrichmentQueued, GameDataStatus.Enriching, GameDataStatus.Enriched, GameDataStatus.Complete],
            GameDataStatus.Failed => [GameDataStatus.EnrichmentQueued, GameDataStatus.Enriching, GameDataStatus.Failed],
            _ => throw new ArgumentOutOfRangeException(nameof(target))
        };
    }

    #endregion
}
