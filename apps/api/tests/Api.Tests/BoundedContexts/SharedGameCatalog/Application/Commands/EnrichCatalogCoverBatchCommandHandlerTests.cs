using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCoverBatch;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Issue #2123 — TDD unit tests for the batch wrapper around the M8 single-entry
/// orchestrator. The batch dispatches one <see cref="EnrichCatalogCoverCommand"/>
/// per game id through <see cref="IMediator"/> and aggregates the discriminated
/// results into total counters + per-game outcomes.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class EnrichCatalogCoverBatchCommandHandlerTests
{
    private readonly Mock<IMediator> _mediator = new();

    private EnrichCatalogCoverBatchCommandHandler CreateHandler() =>
        new(_mediator.Object, NullLogger<EnrichCatalogCoverBatchCommandHandler>.Instance);

    [Fact]
    public async Task Handle_DispatchesOneSingleEntryCommandPerGameId()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        _mediator
            .Setup(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Skipped("qid-missing"));

        var result = await CreateHandler().Handle(
            new EnrichCatalogCoverBatchCommand(ids), CancellationToken.None);

        result.TotalRequested.Should().Be(3);
        _mediator.Verify(m => m.Send(
            It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        foreach (var id in ids)
        {
            _mediator.Verify(m => m.Send(
                It.Is<EnrichCatalogCoverCommand>(c => c.GameId == id), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    [Fact]
    public async Task Handle_AggregatesCountersByOutcomeKind()
    {
        var success = Guid.NewGuid();
        var skipped = Guid.NewGuid();
        var failed = Guid.NewGuid();

        _mediator
            .Setup(m => m.Send(It.Is<EnrichCatalogCoverCommand>(c => c.GameId == success), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("k", "CC BY-SA 4.0", "Author", "https://w/Q1"));
        _mediator
            .Setup(m => m.Send(It.Is<EnrichCatalogCoverCommand>(c => c.GameId == skipped), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Skipped("qid-missing"));
        _mediator
            .Setup(m => m.Send(It.Is<EnrichCatalogCoverCommand>(c => c.GameId == failed), It.IsAny<CancellationToken>()))
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
        _mediator
            .Setup(m => m.Send(It.Is<EnrichCatalogCoverCommand>(c => c.GameId == a), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("ka", "CC0", null, "https://w/Qa"));
        _mediator
            .Setup(m => m.Send(It.Is<EnrichCatalogCoverCommand>(c => c.GameId == b), It.IsAny<CancellationToken>()))
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
    public async Task Handle_TreatsHandlerExceptionAsFailed_DoesNotPropagate()
    {
        // Defensive: an unhandled exception inside the M8 handler MUST NOT
        // abort the batch — each game's outcome is independent. The batch
        // catches and records as "Failed(unhandled-exception)" so ops can
        // still see partial progress on the other games.
        var ok = Guid.NewGuid();
        var bad = Guid.NewGuid();
        _mediator
            .Setup(m => m.Send(It.Is<EnrichCatalogCoverCommand>(c => c.GameId == ok), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("k", "CC0", null, "https://w/Q"));
        _mediator
            .Setup(m => m.Send(It.Is<EnrichCatalogCoverCommand>(c => c.GameId == bad), It.IsAny<CancellationToken>()))
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
        _mediator
            .Setup(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()))
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
}
