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

    // Phase F (#2255) note — Task 2.3 changed the repo return type from
    // WikidataCoverEnrichmentAttempt[] to WikidataAttemptTimelineRow[] so the
    // F6 admin LEFT JOIN result is included in a single round-trip. Mocks
    // updated accordingly; the F6 admin DTO surface (TriggeredByAdminUserId +
    // TriggeredByAdminFullName) lands on the timeline node in Task 3.4.

    private static WikidataAttemptTimelineRow Row(
        Guid attemptId,
        DateTime attemptedAt,
        WikidataCoverEnrichmentOutcome outcome,
        string reason,
        string? details = null,
        int retryCount = 0,
        DateTime? nextRetryAt = null,
        DateTime? deadLetteredAt = null,
        Guid? triggeredByAdminUserId = null,
        string? triggeredByAdminFullName = null) =>
        new(attemptId, attemptedAt, outcome, reason, details, retryCount,
            nextRetryAt, deadLetteredAt, triggeredByAdminUserId, triggeredByAdminFullName);

    [Fact]
    public async Task Handle_NoAttempts_ReturnsEmptyTimeline()
    {
        var gameId = Guid.NewGuid();
        _attempts.Setup(r => r.GetAttemptsByGameIdAsync(gameId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<WikidataAttemptTimelineRow>());

        var result = await Sut().Handle(new GetWikidataAttemptTimelineQuery(gameId, 50), default);

        result.GameId.Should().Be(gameId);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MixedAttempts_MapsAllFieldsAndOutcomes()
    {
        var gameId = Guid.NewGuid();
        var success = Row(Guid.NewGuid(), FixedNow,
            WikidataCoverEnrichmentOutcome.Success, "success");
        var skipped = Row(Guid.NewGuid(), FixedNow.AddMinutes(-5),
            WikidataCoverEnrichmentOutcome.Skipped, "qid-missing");
        var failedWithRetry = Row(Guid.NewGuid(), FixedNow.AddMinutes(-10),
            WikidataCoverEnrichmentOutcome.Failed, "r2-upload-error",
            details: "503", retryCount: 1, nextRetryAt: FixedNow.AddMinutes(-5));
        var deadLetter = Row(Guid.NewGuid(), FixedNow.AddMinutes(-15),
            WikidataCoverEnrichmentOutcome.DeadLetter, "image-processing-error",
            details: "corrupted", deadLetteredAt: FixedNow.AddMinutes(-15));

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
        var newer = Row(Guid.NewGuid(), FixedNow,
            WikidataCoverEnrichmentOutcome.Success, "success");
        var older = Row(Guid.NewGuid(), FixedNow.AddDays(-1),
            WikidataCoverEnrichmentOutcome.Skipped, "qid-missing");

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
            .ReturnsAsync(Array.Empty<WikidataAttemptTimelineRow>())
            .Verifiable();

        await Sut().Handle(new GetWikidataAttemptTimelineQuery(gameId, 7), default);

        _attempts.Verify();
    }
}
