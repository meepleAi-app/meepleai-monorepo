using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.BoundedContexts.SharedGameCatalog.Application.Jobs;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// Unit tests for <see cref="WikidataCoverEnrichmentJob"/>. Post-M12 refactor:
/// the job is a thin batch-loop wrapper around <see cref="IWikidataCoverEnrichmentRunner"/>.
/// These tests verify the batch-loop semantics (no-op when nothing due, per-item
/// exception isolation, throttle index logic, graceful cancellation). Outcome
/// classification + attempt recording is tested in
/// <c>WikidataCoverEnrichmentRunnerTests</c>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public class WikidataCoverEnrichmentJobTests
{
    private static readonly DateTime FixedNow = new(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IWikidataCoverEnrichmentAttemptRepository> _attempts = new();
    private readonly Mock<IWikidataCoverEnrichmentRunner> _runner = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(FixedNow, TimeSpan.Zero));

    private WikidataCoverEnrichmentJob Sut() => new(
        Mock.Of<IServiceProvider>(),
        _time,
        NullLogger<WikidataCoverEnrichmentJob>.Instance);

    [Fact]
    public async Task RunBatchAsync_NoGamesDue_NoOp()
    {
        _attempts.Setup(r => r.GetGameIdsDueForEnrichmentAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        await Sut().RunBatchAsync(_attempts.Object, _runner.Object, default);

        _runner.Verify(
            r => r.EnrichAndRecordAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunBatchAsync_ForwardsForceRefreshFalseAlways()
    {
        var game = Guid.NewGuid();

        _attempts.Setup(r => r.GetGameIdsDueForEnrichmentAsync(
                It.IsAny<int>(), FixedNow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { game });

        _runner.Setup(r => r.EnrichAndRecordAsync(game, false, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("k", "CC0", null, "u"));

        await Sut().RunBatchAsync(_attempts.Object, _runner.Object, default);

        // F6 #1823 Phase F: scheduler passes triggeredByAdminUserId=null (default
        // value) because the cron has no admin actor — the runner stamps null
        // on the attempt row so the F6 timeline drawer can distinguish
        // scheduler-authored attempts from manual admin dispatches.
        _runner.Verify(
            r => r.EnrichAndRecordAsync(game, false, (Guid?)null, It.IsAny<CancellationToken>()),
            Times.Once,
            "scheduler MUST always pass forceRefresh=false AND triggeredByAdminUserId=null");
    }

    [Fact]
    public async Task RunBatchAsync_PerItemException_ContinuesProcessingOthers()
    {
        var game1 = Guid.NewGuid();
        var game2 = Guid.NewGuid();

        _attempts.Setup(r => r.GetGameIdsDueForEnrichmentAsync(
                It.IsAny<int>(), FixedNow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { game1, game2 });

        _runner.Setup(r => r.EnrichAndRecordAsync(game1, false, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        _runner.Setup(r => r.EnrichAndRecordAsync(game2, false, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("k", "CC0", null, "u"));

        await Sut().RunBatchAsync(_attempts.Object, _runner.Object, default);

        _runner.Verify(r => r.EnrichAndRecordAsync(game2, false, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once,
            "game1 throw should be swallowed (logged); the loop continues with game2");
    }

    [Fact]
    public async Task RunBatchAsync_TokenCancelledMidBatch_BreaksLoopGracefully()
    {
        var game1 = Guid.NewGuid();
        var game2 = Guid.NewGuid();
        var cts = new CancellationTokenSource();

        _attempts.Setup(r => r.GetGameIdsDueForEnrichmentAsync(
                It.IsAny<int>(), FixedNow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { game1, game2 });

        _runner.Setup(r => r.EnrichAndRecordAsync(game1, false, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("k", "CC0", null, "u"));

        await Sut().RunBatchAsync(_attempts.Object, _runner.Object, cts.Token);

        _runner.Verify(r => r.EnrichAndRecordAsync(game2, false, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never,
            "after game1 finishes (and cancels the token), the loop top check breaks before game2");
    }
}
