using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Unit tests for <see cref="GetWikidataAttemptTimelineQueryHandler"/>.
/// Issue #1823 Phase E F3.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public class GetWikidataAttemptTimelineQueryHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IWikidataCoverEnrichmentAttemptRepository> _attempts = new();

    private GetWikidataAttemptTimelineQueryHandler Sut() => new(_attempts.Object);

    [Fact]
    public async Task Handle_NoAttempts_ReturnsEmptyTimeline()
    {
        var gameId = Guid.NewGuid();
        _attempts.Setup(r => r.GetAttemptsByGameIdAsync(gameId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<WikidataCoverEnrichmentAttempt>());

        var result = await Sut().Handle(new GetWikidataAttemptTimelineQuery(gameId, 50), default);

        result.GameId.Should().Be(gameId);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MixedAttempts_MapsAllFieldsAndOutcomes()
    {
        var gameId = Guid.NewGuid();
        var success = WikidataCoverEnrichmentAttempt.RecordSuccess(gameId, retryCount: 0, attemptedAt: FixedNow);
        var skipped = WikidataCoverEnrichmentAttempt.RecordSkipped(gameId, "qid-missing",
            retryCount: 0, attemptedAt: FixedNow.AddMinutes(-5));
        var failedWithRetry = WikidataCoverEnrichmentAttempt.RecordFailedWithRetry(
            gameId, "r2-upload-error", "503",
            retryCount: 1, attemptedAt: FixedNow.AddMinutes(-10), nextRetryAt: FixedNow.AddMinutes(-5));
        var deadLetter = WikidataCoverEnrichmentAttempt.RecordDeadLetter(
            gameId, "image-processing-error", "corrupted",
            retryCount: 0, attemptedAt: FixedNow.AddMinutes(-15));

        _attempts.Setup(r => r.GetAttemptsByGameIdAsync(gameId, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { success, skipped, failedWithRetry, deadLetter });

        var result = await Sut().Handle(new GetWikidataAttemptTimelineQuery(gameId, 50), default);

        result.Items.Should().HaveCount(4);
        result.Items.Should().Contain(n => n.Outcome == "Success");
        result.Items.Should().Contain(n => n.Outcome == "Skipped" && n.Reason == "qid-missing");
        result.Items.Should().Contain(n => n.Outcome == "Failed" && n.NextRetryAt.HasValue && n.RetryCount == 1);
        result.Items.Should().Contain(n => n.Outcome == "DeadLetter" && n.DeadLetteredAt.HasValue);
    }

    [Fact]
    public async Task Handle_PreservesRepoOrdering()
    {
        // Repo guarantees DESC AttemptedAt; the handler MUST NOT re-order so
        // the drawer renders the most-recent attempt at the top of the timeline.
        var gameId = Guid.NewGuid();
        var newer = WikidataCoverEnrichmentAttempt.RecordSuccess(gameId, 0, FixedNow);
        var older = WikidataCoverEnrichmentAttempt.RecordSkipped(gameId, "qid-missing", 0, FixedNow.AddDays(-1));

        _attempts.Setup(r => r.GetAttemptsByGameIdAsync(gameId, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { newer, older });

        var result = await Sut().Handle(new GetWikidataAttemptTimelineQuery(gameId, 50), default);

        result.Items[0].Id.Should().Be(newer.Id);
        result.Items[1].Id.Should().Be(older.Id);
    }

    [Fact]
    public async Task Handle_LimitForwardedToRepo()
    {
        var gameId = Guid.NewGuid();
        _attempts.Setup(r => r.GetAttemptsByGameIdAsync(gameId, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<WikidataCoverEnrichmentAttempt>())
            .Verifiable();

        await Sut().Handle(new GetWikidataAttemptTimelineQuery(gameId, 7), default);

        _attempts.Verify();
    }
}
