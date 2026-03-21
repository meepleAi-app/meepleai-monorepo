using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

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

        game.Title.Should().Be("Catan");
        game.GameDataStatus.Should().Be(GameDataStatus.Skeleton);
        game.Status.Should().Be(GameStatus.Draft);
        game.YearPublished.Should().Be(0);
        game.MinPlayers.Should().Be(0);
        game.MaxPlayers.Should().Be(0);
        game.PlayingTimeMinutes.Should().Be(0);
        game.MinAge.Should().Be(0);
        game.ComplexityRating.Should().BeNull();
        game.AverageRating.Should().BeNull();
        game.ImageUrl.Should().Be(string.Empty);
        game.ThumbnailUrl.Should().Be(string.Empty);
        game.Rules.Should().BeNull();
        game.IsDeleted.Should().BeFalse();
        game.HasUploadedPdf.Should().BeFalse();
        game.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void CreateSkeleton_WithBggId_ShouldSetBggId()
    {
        var game = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider, bggId: 13);

        game.BggId.Should().Be(13);
    }

    [Fact]
    public void CreateSkeleton_WithEmptyTitle_ShouldThrow()
    {
        var act = () =>
            SharedGame.CreateSkeleton("", AdminUserId, TimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateSkeleton_WithWhitespaceTitle_ShouldThrow()
    {
        var act = () =>
            SharedGame.CreateSkeleton("   ", AdminUserId, TimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateSkeleton_WithTitleOver500Chars_ShouldThrow()
    {
        var act = () =>
            SharedGame.CreateSkeleton(new string('x', 501), AdminUserId, TimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateSkeleton_WithEmptyCreatedBy_ShouldThrow()
    {
        var act = () =>
            SharedGame.CreateSkeleton("Catan", Guid.Empty, TimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateSkeleton_ShouldTrimTitle()
    {
        var game = SharedGame.CreateSkeleton("  Catan  ", AdminUserId, TimeProvider);

        game.Title.Should().Be("Catan");
    }

    [Fact]
    public void CreateSkeleton_ShouldSetTimestamps()
    {
        var before = DateTime.UtcNow;
        var game = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider);
        var after = DateTime.UtcNow;

        game.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
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

        game.GameDataStatus.Should().Be(to);
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

        var act = () =>
            game.TransitionDataStatusTo(to);
        act.Should().Throw<InvalidOperationException>();
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

        game.GameDataStatus.Should().Be(GameDataStatus.Enriched);
        game.YearPublished.Should().Be(1995);
        game.Description.Should().Be("Trade, build, settle");
        game.MinPlayers.Should().Be(3);
        game.MaxPlayers.Should().Be(4);
        game.PlayingTimeMinutes.Should().Be(90);
        game.MinAge.Should().Be(10);
        game.ComplexityRating.Should().Be(2.3m);
        game.AverageRating.Should().Be(7.2m);
        game.ImageUrl.Should().Be("https://cf.geekdo-images.com/catan.jpg");
        game.ThumbnailUrl.Should().Be("https://cf.geekdo-images.com/catan_t.jpg");
        game.Rules.Should().NotBeNull();
        game.Rules!.ExternalUrl.Should().Be("https://example.com/catan-rules.pdf");
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

        game.Rules.Should().BeNull();
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

        game.BggId.Should().Be(42);
    }

    [Fact]
    public void EnrichFromBgg_NotInEnrichingState_ShouldThrow()
    {
        var game = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider);

        var act = () =>
            game.EnrichFromBgg(
                description: "desc", yearPublished: 2020,
                minPlayers: 2, maxPlayers: 4,
                playingTimeMinutes: 60, minAge: 8,
                complexityRating: null, averageRating: null,
                imageUrl: "https://example.com/img.jpg",
                thumbnailUrl: "https://example.com/thumb.jpg",
                rulebookUrl: null);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EnrichFromBgg_WithInvalidData_ShouldThrowValidation()
    {
        var game = CreateGameInStatus(GameDataStatus.Enriching);

        // minPlayers = 0 should fail validation
        var act = () =>
            game.EnrichFromBgg(
                description: "desc", yearPublished: 2020,
                minPlayers: 0, maxPlayers: 4,
                playingTimeMinutes: 60, minAge: 8,
                complexityRating: null, averageRating: null,
                imageUrl: "https://example.com/img.jpg",
                thumbnailUrl: "https://example.com/thumb.jpg",
                rulebookUrl: null);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region MarkDataComplete

    [Fact]
    public void MarkDataComplete_FromEnriched_ShouldTransition()
    {
        var game = CreateGameInStatus(GameDataStatus.Enriched);

        game.MarkDataComplete();

        game.GameDataStatus.Should().Be(GameDataStatus.Complete);
    }

    [Fact]
    public void MarkDataComplete_FromSkeleton_ShouldThrow()
    {
        var game = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider);

        var act = () => game.MarkDataComplete();
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region SubmitForApproval with GameDataStatus guard

    [Fact]
    public void SubmitForApproval_SkeletonGame_ShouldThrow()
    {
        var game = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider);

        var act = () =>
            game.SubmitForApproval(AdminUserId);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SubmitForApproval_EnrichedGame_ShouldSucceed()
    {
        var game = CreateGameInStatus(GameDataStatus.Enriched);

        game.SubmitForApproval(AdminUserId);

        game.Status.Should().Be(GameStatus.PendingApproval);
    }

    #endregion

    #region SetHasUploadedPdf

    [Fact]
    public void SetHasUploadedPdf_ShouldSetFlagToTrue()
    {
        var game = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider);

        game.SetHasUploadedPdf();

        game.HasUploadedPdf.Should().BeTrue();
    }

    #endregion

    #region Failed → Re-enqueue

    [Fact]
    public void Failed_CanBeReenqueued()
    {
        var game = CreateGameInStatus(GameDataStatus.Failed);

        game.TransitionDataStatusTo(GameDataStatus.EnrichmentQueued);

        game.GameDataStatus.Should().Be(GameDataStatus.EnrichmentQueued);
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
