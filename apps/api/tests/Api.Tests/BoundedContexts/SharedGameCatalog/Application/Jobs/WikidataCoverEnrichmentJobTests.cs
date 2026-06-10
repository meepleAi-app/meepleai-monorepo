using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.BoundedContexts.SharedGameCatalog.Application.Jobs;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// Unit tests for <see cref="WikidataCoverEnrichmentJob"/> — Issue #1823 Wave 3 M9.
/// Drives <see cref="WikidataCoverEnrichmentJob.RunBatchAsync"/> directly with
/// mocked dependencies so the Quartz scheduler is not needed. Uses the real
/// <see cref="WikidataCoverEnrichmentRetryPolicy"/> to exercise the full DEC-3j
/// classification flow.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public class WikidataCoverEnrichmentJobTests
{
    private static readonly DateTime FixedNow = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IWikidataCoverEnrichmentAttemptRepository> _attempts = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly WikidataCoverEnrichmentRetryPolicy _policy = new();
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

        await Sut().RunBatchAsync(_attempts.Object, _uow.Object, _mediator.Object, _policy, default);

        _mediator.Verify(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunBatchAsync_SuccessOutcome_RecordsSuccessAttempt()
    {
        var gameId = Guid.NewGuid();

        _attempts.Setup(r => r.GetGameIdsDueForEnrichmentAsync(
                It.IsAny<int>(), FixedNow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });

        _attempts.Setup(r => r.GetLatestBySharedGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WikidataCoverEnrichmentAttempt?)null);

        _mediator.Setup(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("key", "CC0", null, "url"));

        WikidataCoverEnrichmentAttempt? recorded = null;
        _attempts.Setup(r => r.AddAsync(It.IsAny<WikidataCoverEnrichmentAttempt>(), It.IsAny<CancellationToken>()))
            .Callback<WikidataCoverEnrichmentAttempt, CancellationToken>((a, _) => recorded = a)
            .Returns(Task.CompletedTask);

        await Sut().RunBatchAsync(_attempts.Object, _uow.Object, _mediator.Object, _policy, default);

        recorded.Should().NotBeNull();
        recorded!.Outcome.Should().Be(WikidataCoverEnrichmentOutcome.Success);
        recorded.Reason.Should().Be("success");
        recorded.RetryCount.Should().Be(0, "first attempt, no previous retries");
        recorded.NextRetryAt.Should().BeNull();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunBatchAsync_SkippedOutcome_RecordsSkippedAttempt()
    {
        var gameId = Guid.NewGuid();

        _attempts.Setup(r => r.GetGameIdsDueForEnrichmentAsync(
                It.IsAny<int>(), FixedNow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });
        _attempts.Setup(r => r.GetLatestBySharedGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WikidataCoverEnrichmentAttempt?)null);

        _mediator.Setup(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Skipped(EnrichCatalogCoverCommandHandler.SkipReasonQidMissing));

        WikidataCoverEnrichmentAttempt? recorded = null;
        _attempts.Setup(r => r.AddAsync(It.IsAny<WikidataCoverEnrichmentAttempt>(), It.IsAny<CancellationToken>()))
            .Callback<WikidataCoverEnrichmentAttempt, CancellationToken>((a, _) => recorded = a)
            .Returns(Task.CompletedTask);

        await Sut().RunBatchAsync(_attempts.Object, _uow.Object, _mediator.Object, _policy, default);

        recorded!.Outcome.Should().Be(WikidataCoverEnrichmentOutcome.Skipped);
        recorded.Reason.Should().Be(EnrichCatalogCoverCommandHandler.SkipReasonQidMissing);
        recorded.NextRetryAt.Should().BeNull("skipped is terminal");
    }

    [Fact]
    public async Task RunBatchAsync_FailedR2Upload_FirstAttempt_SchedulesRetryAt1m()
    {
        var gameId = Guid.NewGuid();

        _attempts.Setup(r => r.GetGameIdsDueForEnrichmentAsync(
                It.IsAny<int>(), FixedNow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });
        _attempts.Setup(r => r.GetLatestBySharedGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WikidataCoverEnrichmentAttempt?)null);

        _mediator.Setup(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Failed(
                EnrichCatalogCoverCommandHandler.FailReasonR2Upload, "503"));

        WikidataCoverEnrichmentAttempt? recorded = null;
        _attempts.Setup(r => r.AddAsync(It.IsAny<WikidataCoverEnrichmentAttempt>(), It.IsAny<CancellationToken>()))
            .Callback<WikidataCoverEnrichmentAttempt, CancellationToken>((a, _) => recorded = a)
            .Returns(Task.CompletedTask);

        await Sut().RunBatchAsync(_attempts.Object, _uow.Object, _mediator.Object, _policy, default);

        recorded!.Outcome.Should().Be(WikidataCoverEnrichmentOutcome.Failed);
        recorded.RetryCount.Should().Be(1, "first retry");
        recorded.NextRetryAt.Should().Be(FixedNow.AddMinutes(1), "DEC-3j: 1m for the first retry");
    }

    [Fact]
    public async Task RunBatchAsync_FailedR2Upload_AfterMaxRetries_DeadLetters()
    {
        var gameId = Guid.NewGuid();

        // Previous attempt: 3 retries already exhausted
        var previous = WikidataCoverEnrichmentAttempt.RecordFailedWithRetry(
            gameId, EnrichCatalogCoverCommandHandler.FailReasonR2Upload, "503",
            retryCount: 3, attemptedAt: FixedNow.AddMinutes(-20), nextRetryAt: FixedNow.AddMinutes(-5));

        _attempts.Setup(r => r.GetGameIdsDueForEnrichmentAsync(
                It.IsAny<int>(), FixedNow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });
        _attempts.Setup(r => r.GetLatestBySharedGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(previous);

        _mediator.Setup(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Failed(
                EnrichCatalogCoverCommandHandler.FailReasonR2Upload, "503"));

        WikidataCoverEnrichmentAttempt? recorded = null;
        _attempts.Setup(r => r.AddAsync(It.IsAny<WikidataCoverEnrichmentAttempt>(), It.IsAny<CancellationToken>()))
            .Callback<WikidataCoverEnrichmentAttempt, CancellationToken>((a, _) => recorded = a)
            .Returns(Task.CompletedTask);

        await Sut().RunBatchAsync(_attempts.Object, _uow.Object, _mediator.Object, _policy, default);

        recorded!.Outcome.Should().Be(WikidataCoverEnrichmentOutcome.DeadLetter);
        recorded.DeadLetteredAt.Should().Be(FixedNow);
        recorded.NextRetryAt.Should().BeNull("dead-letter is terminal");
        recorded.RetryCount.Should().Be(3, "preserve previous retry count on terminal");
    }

    [Fact]
    public async Task RunBatchAsync_FailedImageProcessing_DeadLettersImmediately()
    {
        var gameId = Guid.NewGuid();

        _attempts.Setup(r => r.GetGameIdsDueForEnrichmentAsync(
                It.IsAny<int>(), FixedNow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });
        _attempts.Setup(r => r.GetLatestBySharedGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WikidataCoverEnrichmentAttempt?)null);

        _mediator.Setup(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Failed(
                EnrichCatalogCoverCommandHandler.FailReasonImageProcessing, "corrupted bytes"));

        WikidataCoverEnrichmentAttempt? recorded = null;
        _attempts.Setup(r => r.AddAsync(It.IsAny<WikidataCoverEnrichmentAttempt>(), It.IsAny<CancellationToken>()))
            .Callback<WikidataCoverEnrichmentAttempt, CancellationToken>((a, _) => recorded = a)
            .Returns(Task.CompletedTask);

        await Sut().RunBatchAsync(_attempts.Object, _uow.Object, _mediator.Object, _policy, default);

        recorded!.Outcome.Should().Be(WikidataCoverEnrichmentOutcome.DeadLetter);
        recorded.Reason.Should().Be(EnrichCatalogCoverCommandHandler.FailReasonImageProcessing);
        recorded.RetryCount.Should().Be(0, "no retries attempted for corrupted-image failure");
    }

    [Fact]
    public async Task RunBatchAsync_PerItemException_ContinuesProcessingOthers()
    {
        var game1 = Guid.NewGuid();
        var game2 = Guid.NewGuid();

        _attempts.Setup(r => r.GetGameIdsDueForEnrichmentAsync(
                It.IsAny<int>(), FixedNow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { game1, game2 });

        _attempts.Setup(r => r.GetLatestBySharedGameIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WikidataCoverEnrichmentAttempt?)null);

        // game1 throws inside the mediator; game2 succeeds.
        _mediator.Setup(m => m.Send(It.Is<EnrichCatalogCoverCommand>(c => c.GameId == game1), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        _mediator.Setup(m => m.Send(It.Is<EnrichCatalogCoverCommand>(c => c.GameId == game2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("k", "CC0", null, "u"));

        var recorded = new List<WikidataCoverEnrichmentAttempt>();
        _attempts.Setup(r => r.AddAsync(It.IsAny<WikidataCoverEnrichmentAttempt>(), It.IsAny<CancellationToken>()))
            .Callback<WikidataCoverEnrichmentAttempt, CancellationToken>((a, _) => recorded.Add(a))
            .Returns(Task.CompletedTask);

        await Sut().RunBatchAsync(_attempts.Object, _uow.Object, _mediator.Object, _policy, default);

        recorded.Should().HaveCount(1,
            "game1 throw should be swallowed (no attempt recorded), game2 success records normally");
        recorded[0].SharedGameId.Should().Be(game2);
    }

    [Fact]
    public async Task RunBatchAsync_TokenCancelledMidBatch_BreaksLoopGracefully()
    {
        // Quartz semantics: when shutdown fires, the job's CT is signalled and
        // the batch should drain the current game then stop on the next loop top
        // check (NOT throw out). The test verifies that game2 is skipped after
        // game1 cancels the token.
        var game1 = Guid.NewGuid();
        var game2 = Guid.NewGuid();
        var cts = new CancellationTokenSource();

        _attempts.Setup(r => r.GetGameIdsDueForEnrichmentAsync(
                It.IsAny<int>(), FixedNow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { game1, game2 });

        _attempts.Setup(r => r.GetLatestBySharedGameIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WikidataCoverEnrichmentAttempt?)null);

        _mediator.Setup(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("k", "CC0", null, "u"));

        await Sut().RunBatchAsync(_attempts.Object, _uow.Object, _mediator.Object, _policy, cts.Token);

        _mediator.Verify(
            m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "after game1 finishes (and cancels the token), the loop top check breaks before game2");
    }
}
