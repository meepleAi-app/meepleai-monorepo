using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCoverBatch;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Issue #2123 — TDD unit tests for the batch wrapper around the M8 single-entry
/// orchestrator. Issue #3369: the batch now delegates each game to the
/// <see cref="IWikidataCoverEnrichmentRunner"/> (the SSOT that records an attempt
/// row, applies the DEC-3j retry/dead-letter policy and broadcasts SSE) instead
/// of dispatching the raw <see cref="EnrichCatalogCoverCommand"/> via IMediator.
/// The batch still aggregates the discriminated results into total counters +
/// per-game outcomes and keeps its own per-game exception hierarchy.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class EnrichCatalogCoverBatchCommandHandlerTests
{
    private readonly Mock<IWikidataCoverEnrichmentRunner> _runner = new();

    private EnrichCatalogCoverBatchCommandHandler CreateHandler() =>
        new(_runner.Object, NullLogger<EnrichCatalogCoverBatchCommandHandler>.Instance);

    [Fact]
    public async Task Handle_DelegatesEachGameToTheRunner_ForceRefreshFalse()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        _runner
            .Setup(r => r.EnrichAndRecordAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Skipped("qid-missing"));

        var result = await CreateHandler().Handle(
            new EnrichCatalogCoverBatchCommand(ids), CancellationToken.None);

        result.TotalRequested.Should().Be(3);
        _runner.Verify(r => r.EnrichAndRecordAsync(
            It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        foreach (var id in ids)
        {
            // #3369: each game must go through the runner (attempt-log + retry +
            // dead-letter + SSE parity with the M9/M12 triggers), with forceRefresh
            // false and no admin trigger id — matching the previous raw-command semantics.
            _runner.Verify(r => r.EnrichAndRecordAsync(
                id, false, null, It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public async Task Handle_AggregatesCountersByOutcomeKind()
    {
        var success = Guid.NewGuid();
        var skipped = Guid.NewGuid();
        var failed = Guid.NewGuid();

        _runner
            .Setup(r => r.EnrichAndRecordAsync(success, It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("k", "CC BY-SA 4.0", "Author", "https://w/Q1"));
        _runner
            .Setup(r => r.EnrichAndRecordAsync(skipped, It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Skipped("qid-missing"));
        _runner
            .Setup(r => r.EnrichAndRecordAsync(failed, It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Failed("r2-upload-error", "S3 503"));

        var result = await CreateHandler().Handle(
            new EnrichCatalogCoverBatchCommand(new[] { success, skipped, failed }), CancellationToken.None);

        result.TotalRequested.Should().Be(3);
        result.SuccessCount.Should().Be(1);
        result.SkippedCount.Should().Be(1);
        result.FailedCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_CapturesPerGameOutcomes_InEnumerationOrder()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        _runner
            .Setup(r => r.EnrichAndRecordAsync(a, It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("ka", "CC0", null, "https://w/Qa"));
        _runner
            .Setup(r => r.EnrichAndRecordAsync(b, It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Skipped("license-not-whitelisted"));

        var result = await CreateHandler().Handle(
            new EnrichCatalogCoverBatchCommand(new[] { a, b }), CancellationToken.None);

        result.PerGame.Should().HaveCount(2);
        result.PerGame[0].GameId.Should().Be(a);
        result.PerGame[0].Outcome.Should().Be("success");
        result.PerGame[1].GameId.Should().Be(b);
        result.PerGame[1].Outcome.Should().Be("skipped");
        result.PerGame[1].Reason.Should().Be("license-not-whitelisted");
    }

    [Fact]
    public async Task Handle_TreatsRunnerExceptionAsFailed_DoesNotPropagate()
    {
        // Defensive: an unhandled exception leaking from the runner MUST NOT
        // abort the batch — each game's outcome is independent. The batch
        // catches and records as "Failed(unhandled-exception)" so ops can
        // still see partial progress on the other games.
        var ok = Guid.NewGuid();
        var bad = Guid.NewGuid();
        _runner
            .Setup(r => r.EnrichAndRecordAsync(ok, It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("k", "CC0", null, "https://w/Q"));
        _runner
            .Setup(r => r.EnrichAndRecordAsync(bad, It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await CreateHandler().Handle(
            new EnrichCatalogCoverBatchCommand(new[] { ok, bad }), CancellationToken.None);

        result.TotalRequested.Should().Be(2);
        result.SuccessCount.Should().Be(1);
        result.FailedCount.Should().Be(1);
        result.PerGame.Single(p => p.GameId == bad).Outcome.Should().Be("failed");
        result.PerGame.Single(p => p.GameId == bad).Reason.Should().Be("unhandled-exception");
    }

    [Fact]
    public async Task Handle_RespectsCancellationToken_StopsDispatching()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        using var cts = new CancellationTokenSource();
        var callCount = 0;
        _runner
            .Setup(r => r.EnrichAndRecordAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1) cts.Cancel();
                return new EnrichCatalogCoverResult.Skipped("qid-missing");
            });

        Func<Task> act = () => CreateHandler().Handle(new EnrichCatalogCoverBatchCommand(ids), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        callCount.Should().Be(1, "cancellation must short-circuit before dispatching the second command");
    }

    [Fact]
    public async Task Handle_TreatsTaskCanceledFromTimeoutAsFailed_ContinuesNotPropagates()
    {
        // Issue #2157: defense-in-depth. Even if the runner leaks a
        // TaskCanceledException (e.g. HttpClient.Timeout above the provider
        // guard, or any future code path bypassing the M3/M4 fix), the batch
        // handler MUST distinguish it from a real caller cancellation and
        // continue with the next game. The discriminator is the caller's
        // CancellationToken state: if NOT cancelled, the leak is mapped to a
        // per-game Failed("child-timeout") entry; if cancelled, it propagates.
        var ok = Guid.NewGuid();
        var timedOut = Guid.NewGuid();
        _runner
            .Setup(r => r.EnrichAndRecordAsync(ok, It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("k", "CC0", null, "https://w/Q"));
        _runner
            .Setup(r => r.EnrichAndRecordAsync(timedOut, It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("simulated HTTP timeout leak"));

        using var cts = new CancellationTokenSource(); // NOT cancelled
        var result = await CreateHandler().Handle(
            new EnrichCatalogCoverBatchCommand(new[] { ok, timedOut }), cts.Token);

        result.TotalRequested.Should().Be(2);
        result.SuccessCount.Should().Be(1);
        result.FailedCount.Should().Be(1);
        result.PerGame.Single(p => p.GameId == timedOut).Outcome.Should().Be("failed");
        result.PerGame.Single(p => p.GameId == timedOut).Reason.Should().Be("child-timeout");

        // FailedDetails carries exception drill-down for the admin UI.
        result.FailedDetails.Should().NotBeNull();
        result.FailedDetails!.Should().ContainSingle(d => d.GameId == timedOut);
        var detail = result.FailedDetails!.Single(d => d.GameId == timedOut);
        detail.ExceptionType.Should().Be("TaskCanceledException");
        detail.ExceptionMessage.Should().Contain("HTTP timeout");
    }

    [Fact]
    public async Task Handle_TreatsHttpRequestExceptionAsFailed_RecordsExceptionType()
    {
        // Issue #2157: any HttpRequestException leaked from the runner (e.g. DNS
        // error, connection refused, 5xx that bypasses the provider's catch)
        // maps to a per-game Failed("http-error") entry with a FailedDetail
        // payload carrying the exception type and (sanitised) message so the
        // admin UI can drill down into transient vs permanent failures.
        var ok = Guid.NewGuid();
        var httpFail = Guid.NewGuid();
        _runner
            .Setup(r => r.EnrichAndRecordAsync(ok, It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("k", "CC0", null, "https://w/Q"));
        _runner
            .Setup(r => r.EnrichAndRecordAsync(httpFail, It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("DNS resolution failed", null, System.Net.HttpStatusCode.ServiceUnavailable));

        var result = await CreateHandler().Handle(
            new EnrichCatalogCoverBatchCommand(new[] { ok, httpFail }), CancellationToken.None);

        result.TotalRequested.Should().Be(2);
        result.SuccessCount.Should().Be(1);
        result.FailedCount.Should().Be(1);
        var entry = result.PerGame.Single(p => p.GameId == httpFail);
        entry.Outcome.Should().Be("failed");
        entry.Reason.Should().Be("http-error");

        // FailedDetails populated for drill-down.
        result.FailedDetails.Should().NotBeNull();
        result.FailedDetails!.Should().ContainSingle(d => d.GameId == httpFail);
        var detail = result.FailedDetails!.Single(d => d.GameId == httpFail);
        detail.ExceptionType.Should().Be("HttpRequestException");
        detail.ExceptionMessage.Should().Contain("DNS resolution failed");
    }

    [Fact]
    public async Task Handle_FailedDetailsIsNull_WhenNoFailuresOccur()
    {
        // Backward-compat: existing callers MUST observe FailedDetails == null
        // when every game succeeds (no allocation of empty list). This locks
        // the optional-parameter default to null so any future serialization
        // (JSON to admin UI, audit log) does not change shape on the green path.
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        _runner
            .Setup(r => r.EnrichAndRecordAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("k", "CC0", null, "https://w/Q"));

        var result = await CreateHandler().Handle(
            new EnrichCatalogCoverBatchCommand(new[] { a, b }), CancellationToken.None);

        result.SuccessCount.Should().Be(2);
        result.FailedCount.Should().Be(0);
        result.FailedDetails.Should().BeNull("no exception path was taken — no drill-down payload allocated");
    }
}
