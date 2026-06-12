using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Observability;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Unit tests for <see cref="WikidataCoverEnrichmentRunner"/> — Issue #1823 Wave 3 M12.
/// Single source of truth for the enrich+record workflow used by both the M9
/// scheduler and the M12 admin trigger endpoint. Uses the real
/// <see cref="WikidataCoverEnrichmentRetryPolicy"/> (no mock policy) per the
/// <c>acceptance_tests_must_exercise_real_pipeline</c> feedback memory.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public class WikidataCoverEnrichmentRunnerTests
{
    private static readonly DateTime FixedNow = new(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IWikidataCoverEnrichmentAttemptRepository> _attempts = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly WikidataCoverEnrichmentRetryPolicy _policy = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(FixedNow, TimeSpan.Zero));
    private readonly Mock<IWikidataEnrichmentEventBroadcaster> _broadcaster = new();

    private WikidataCoverEnrichmentRunner Sut() => new(
        _mediator.Object, _attempts.Object, _uow.Object, _policy,
        _time, _broadcaster.Object,
        NullLogger<WikidataCoverEnrichmentRunner>.Instance);

    [Fact]
    public async Task EnrichAndRecordAsync_SuccessOutcome_RecordsSuccessAttempt()
    {
        var gameId = Guid.NewGuid();

        _attempts.Setup(r => r.GetLatestBySharedGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WikidataCoverEnrichmentAttempt?)null);

        _mediator.Setup(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("key", "CC0", null, "url"));

        WikidataCoverEnrichmentAttempt? recorded = null;
        _attempts.Setup(r => r.AddAsync(It.IsAny<WikidataCoverEnrichmentAttempt>(), It.IsAny<CancellationToken>()))
            .Callback<WikidataCoverEnrichmentAttempt, CancellationToken>((a, _) => recorded = a)
            .Returns(Task.CompletedTask);

        var result = await Sut().EnrichAndRecordAsync(gameId, forceRefresh: false, default);

        result.Should().BeOfType<EnrichCatalogCoverResult.Success>();
        recorded.Should().NotBeNull();
        recorded!.Outcome.Should().Be(WikidataCoverEnrichmentOutcome.Success);
        recorded.RetryCount.Should().Be(0);
        recorded.NextRetryAt.Should().BeNull();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnrichAndRecordAsync_SkippedOutcome_RecordsSkippedAttempt()
    {
        var gameId = Guid.NewGuid();

        _attempts.Setup(r => r.GetLatestBySharedGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WikidataCoverEnrichmentAttempt?)null);

        _mediator.Setup(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Skipped(EnrichCatalogCoverCommandHandler.SkipReasonQidMissing));

        WikidataCoverEnrichmentAttempt? recorded = null;
        _attempts.Setup(r => r.AddAsync(It.IsAny<WikidataCoverEnrichmentAttempt>(), It.IsAny<CancellationToken>()))
            .Callback<WikidataCoverEnrichmentAttempt, CancellationToken>((a, _) => recorded = a)
            .Returns(Task.CompletedTask);

        await Sut().EnrichAndRecordAsync(gameId, forceRefresh: false, default);

        recorded!.Outcome.Should().Be(WikidataCoverEnrichmentOutcome.Skipped);
        recorded.Reason.Should().Be(EnrichCatalogCoverCommandHandler.SkipReasonQidMissing);
    }

    [Fact]
    public async Task EnrichAndRecordAsync_FailedR2Upload_FirstAttempt_SchedulesRetryAt1m()
    {
        var gameId = Guid.NewGuid();

        _attempts.Setup(r => r.GetLatestBySharedGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WikidataCoverEnrichmentAttempt?)null);

        _mediator.Setup(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Failed(
                EnrichCatalogCoverCommandHandler.FailReasonR2Upload, "503"));

        WikidataCoverEnrichmentAttempt? recorded = null;
        _attempts.Setup(r => r.AddAsync(It.IsAny<WikidataCoverEnrichmentAttempt>(), It.IsAny<CancellationToken>()))
            .Callback<WikidataCoverEnrichmentAttempt, CancellationToken>((a, _) => recorded = a)
            .Returns(Task.CompletedTask);

        await Sut().EnrichAndRecordAsync(gameId, forceRefresh: false, default);

        recorded!.Outcome.Should().Be(WikidataCoverEnrichmentOutcome.Failed);
        recorded.RetryCount.Should().Be(1);
        recorded.NextRetryAt.Should().Be(FixedNow.AddMinutes(1));
    }

    [Fact]
    public async Task EnrichAndRecordAsync_AfterMaxRetries_DeadLetters()
    {
        var gameId = Guid.NewGuid();
        var previous = WikidataCoverEnrichmentAttempt.RecordFailedWithRetry(
            gameId, EnrichCatalogCoverCommandHandler.FailReasonR2Upload, "503",
            retryCount: 3, attemptedAt: FixedNow.AddMinutes(-20), nextRetryAt: FixedNow.AddMinutes(-5));

        _attempts.Setup(r => r.GetLatestBySharedGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(previous);

        _mediator.Setup(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Failed(
                EnrichCatalogCoverCommandHandler.FailReasonR2Upload, "503"));

        WikidataCoverEnrichmentAttempt? recorded = null;
        _attempts.Setup(r => r.AddAsync(It.IsAny<WikidataCoverEnrichmentAttempt>(), It.IsAny<CancellationToken>()))
            .Callback<WikidataCoverEnrichmentAttempt, CancellationToken>((a, _) => recorded = a)
            .Returns(Task.CompletedTask);

        await Sut().EnrichAndRecordAsync(gameId, forceRefresh: false, default);

        recorded!.Outcome.Should().Be(WikidataCoverEnrichmentOutcome.DeadLetter);
        recorded.DeadLetteredAt.Should().Be(FixedNow);
        recorded.NextRetryAt.Should().BeNull();
        recorded.RetryCount.Should().Be(3, "preserve previous retry count on terminal");
    }

    [Fact]
    public async Task EnrichAndRecordAsync_ImageProcessingError_DeadLettersImmediately()
    {
        var gameId = Guid.NewGuid();

        _attempts.Setup(r => r.GetLatestBySharedGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WikidataCoverEnrichmentAttempt?)null);

        _mediator.Setup(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Failed(
                EnrichCatalogCoverCommandHandler.FailReasonImageProcessing, "corrupted"));

        WikidataCoverEnrichmentAttempt? recorded = null;
        _attempts.Setup(r => r.AddAsync(It.IsAny<WikidataCoverEnrichmentAttempt>(), It.IsAny<CancellationToken>()))
            .Callback<WikidataCoverEnrichmentAttempt, CancellationToken>((a, _) => recorded = a)
            .Returns(Task.CompletedTask);

        await Sut().EnrichAndRecordAsync(gameId, forceRefresh: false, default);

        recorded!.Outcome.Should().Be(WikidataCoverEnrichmentOutcome.DeadLetter);
        recorded.RetryCount.Should().Be(0, "no retries attempted for corrupted-image failure");
    }

    [Fact]
    public async Task EnrichAndRecordAsync_ForceRefreshTrue_ForwardsToCommand()
    {
        var gameId = Guid.NewGuid();
        EnrichCatalogCoverCommand? sent = null;

        _attempts.Setup(r => r.GetLatestBySharedGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WikidataCoverEnrichmentAttempt?)null);

        _mediator.Setup(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<EnrichCatalogCoverResult>, CancellationToken>((c, _) => sent = (EnrichCatalogCoverCommand)c)
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("k", "CC0", null, "u"));

        _attempts.Setup(r => r.AddAsync(It.IsAny<WikidataCoverEnrichmentAttempt>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await Sut().EnrichAndRecordAsync(gameId, forceRefresh: true, default);

        sent.Should().NotBeNull();
        sent!.GameId.Should().Be(gameId);
        sent.ForceRefresh.Should().BeTrue(
            "the admin trigger MUST be able to bypass the M8 freshness window via ForceRefresh");
    }

    [Fact]
    public async Task EnrichAndRecordAsync_CircuitOpen_PreservesRetryCount()
    {
        var gameId = Guid.NewGuid();

        // Previous attempt: 2 retries already burned on r2-upload errors.
        var previous = WikidataCoverEnrichmentAttempt.RecordFailedWithRetry(
            gameId, EnrichCatalogCoverCommandHandler.FailReasonR2Upload, "503",
            retryCount: 2, attemptedAt: FixedNow.AddMinutes(-30), nextRetryAt: FixedNow.AddMinutes(-10));

        _attempts.Setup(r => r.GetLatestBySharedGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(previous);

        _mediator.Setup(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Failed(
                EnrichCatalogCoverCommandHandler.FailReasonCircuitOpen, "circuit OPEN"));

        WikidataCoverEnrichmentAttempt? recorded = null;
        _attempts.Setup(r => r.AddAsync(It.IsAny<WikidataCoverEnrichmentAttempt>(), It.IsAny<CancellationToken>()))
            .Callback<WikidataCoverEnrichmentAttempt, CancellationToken>((a, _) => recorded = a)
            .Returns(Task.CompletedTask);

        await Sut().EnrichAndRecordAsync(gameId, forceRefresh: false, default);

        recorded!.RetryCount.Should().Be(2,
            "circuit-open is an upstream-infra trip — the runner MUST NOT burn another DEC-3j retry budget slot");
        recorded.NextRetryAt.Should().NotBeNull("circuit-open schedules a retry past the breaker window");
    }

    [Fact]
    public async Task EnrichAndRecordAsync_ReturnsCommandResultDirectly()
    {
        var gameId = Guid.NewGuid();
        var expected = new EnrichCatalogCoverResult.Success("custom-key", "CC BY 4.0", "Jane", "src");

        _attempts.Setup(r => r.GetLatestBySharedGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WikidataCoverEnrichmentAttempt?)null);
        _mediator.Setup(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        _attempts.Setup(r => r.AddAsync(It.IsAny<WikidataCoverEnrichmentAttempt>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await Sut().EnrichAndRecordAsync(gameId, forceRefresh: false, default);

        result.Should().BeSameAs(expected,
            "the runner returns the M8 result verbatim so admin callers can map it to a DTO");
    }

    [Fact]
    public async Task EnrichAndRecordAsync_DeadLetterOutcome_IncrementsDeadLetterGauge()
    {
        // F1 #1823 Wave 3: persisting a DeadLetter attempt MUST bump the
        // dead_letter_count gauge so ops dashboards reflect the new triage
        // backlog before the daily retention-sweep re-anchor.
        var gameId = Guid.NewGuid();

        _attempts.Setup(r => r.GetLatestBySharedGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WikidataCoverEnrichmentAttempt?)null);
        _mediator.Setup(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Failed(
                EnrichCatalogCoverCommandHandler.FailReasonImageProcessing, "corrupted"));
        _attempts.Setup(r => r.AddAsync(It.IsAny<WikidataCoverEnrichmentAttempt>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Anchor the gauge so the assertion is independent of cross-test leakage.
        MeepleAiMetrics.SetWikidataDeadLetterCount(5);

        await Sut().EnrichAndRecordAsync(gameId, forceRefresh: false, default);

        ReadGauge(MeepleAiMetrics.WikidataDeadLetterCount).Should().Be(6,
            "F1 hybrid update: runner increments by 1 per persisted DeadLetter attempt");
    }

    [Fact]
    public async Task EnrichAndRecordAsync_PostSaveChanges_PublishesBroadcastEvent()
    {
        // F4 #1823 Phase E: every persisted attempt MUST be broadcast on the
        // SSE pub/sub so the admin dead-letter page updates inline. The
        // publish call MUST happen AFTER SaveChangesAsync — same placement as
        // the F1 dead-letter gauge increment — so a DB write failure cannot
        // leak phantom rows to subscribers.
        var gameId = Guid.NewGuid();

        _attempts.Setup(r => r.GetLatestBySharedGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WikidataCoverEnrichmentAttempt?)null);
        _mediator.Setup(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Failed(
                EnrichCatalogCoverCommandHandler.FailReasonImageProcessing, "corrupted"));
        _attempts.Setup(r => r.AddAsync(It.IsAny<WikidataCoverEnrichmentAttempt>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await Sut().EnrichAndRecordAsync(gameId, forceRefresh: false, default);

        _broadcaster.Verify(b => b.Publish(It.Is<WikidataEnrichmentEvent>(
                e => e.SharedGameId == gameId
                    && e.Outcome == nameof(WikidataCoverEnrichmentOutcome.DeadLetter)
                    && e.Reason == EnrichCatalogCoverCommandHandler.FailReasonImageProcessing)),
            Times.Once);
    }

    [Fact]
    public async Task EnrichAndRecordAsync_FailedRetryOutcome_DoesNotIncrementDeadLetterGauge()
    {
        // F1 #1823 Wave 3: only DeadLetter terminal counts towards the gauge —
        // a transient Failed-with-retry MUST NOT inflate the operator backlog
        // signal.
        var gameId = Guid.NewGuid();

        _attempts.Setup(r => r.GetLatestBySharedGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WikidataCoverEnrichmentAttempt?)null);
        _mediator.Setup(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Failed(
                EnrichCatalogCoverCommandHandler.FailReasonR2Upload, "503"));
        _attempts.Setup(r => r.AddAsync(It.IsAny<WikidataCoverEnrichmentAttempt>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        MeepleAiMetrics.SetWikidataDeadLetterCount(7);

        await Sut().EnrichAndRecordAsync(gameId, forceRefresh: false, default);

        ReadGauge(MeepleAiMetrics.WikidataDeadLetterCount).Should().Be(7,
            "F1: Failed-with-retry is not a terminal — gauge MUST stay anchored at the previous value");
    }

    private static int ReadGauge(System.Diagnostics.Metrics.ObservableGauge<int> gauge)
    {
        var collected = new List<int>();
        using var listener = new System.Diagnostics.Metrics.MeterListener
        {
            InstrumentPublished = (instr, l) =>
            {
                if (instr == gauge) l.EnableMeasurementEvents(instr);
            },
        };
        listener.SetMeasurementEventCallback<int>((_, value, _, _) => collected.Add(value));
        listener.Start();
        listener.RecordObservableInstruments();
        return collected[^1];
    }
}
