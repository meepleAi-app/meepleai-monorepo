using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;

/// <summary>
/// Unit tests for <see cref="AdminBulkRetryWikidataCoverCommandHandler"/>.
/// Issue #1823 Phase E F2.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public class AdminBulkRetryWikidataCoverCommandHandlerTests
{
    private readonly Mock<IWikidataCoverEnrichmentAttemptRepository> _attempts = new();
    private readonly Mock<IWikidataCoverEnrichmentRunner> _runner = new();

    private AdminBulkRetryWikidataCoverCommandHandler Sut() => new(
        _attempts.Object,
        _runner.Object,
        NullLogger<AdminBulkRetryWikidataCoverCommandHandler>.Instance);

    [Fact]
    public async Task Handle_AllIdsResolved_DispatchesEachWithForceRefreshTrue()
    {
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();

        _attempts.Setup(r => r.GetSharedGameIdsByAttemptIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, Guid> { [a1] = g1, [a2] = g2 });

        _runner.Setup(r => r.EnrichAndRecordAsync(
                It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("k", "CC0", null, "u"));

        var result = await Sut().Handle(
            new AdminBulkRetryWikidataCoverCommand(
                AttemptIds: new[] { a1, a2 },
                TriggeredByUserId: Guid.NewGuid()),
            default);

        result.TriggeredCount.Should().Be(2);
        result.NotFoundCount.Should().Be(0);
        result.FailedCount.Should().Be(0);
        result.Rows.Should().HaveCount(2);
        result.Rows.Should().AllSatisfy(row => row.Outcome.Should().Be(AdminBulkRetryRow.OutcomeTriggered));
        _runner.Verify(r => r.EnrichAndRecordAsync(g1, true, It.IsAny<CancellationToken>()), Times.Once);
        _runner.Verify(r => r.EnrichAndRecordAsync(g2, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UnknownAttemptId_ProducesNotFoundRowWithoutInvokingRunner()
    {
        var known = Guid.NewGuid();
        var unknown = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _attempts.Setup(r => r.GetSharedGameIdsByAttemptIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, Guid> { [known] = gameId });

        _runner.Setup(r => r.EnrichAndRecordAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("k", "CC0", null, "u"));

        var result = await Sut().Handle(
            new AdminBulkRetryWikidataCoverCommand(
                AttemptIds: new[] { known, unknown },
                TriggeredByUserId: Guid.NewGuid()),
            default);

        result.TriggeredCount.Should().Be(1);
        result.NotFoundCount.Should().Be(1);
        result.FailedCount.Should().Be(0);
        result.Rows.Single(r => r.AttemptId == unknown).Outcome.Should().Be(AdminBulkRetryRow.OutcomeNotFound);
        result.Rows.Single(r => r.AttemptId == unknown).GameId.Should().BeNull();
        _runner.Verify(r => r.EnrichAndRecordAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()), Times.Once,
            "the unknown id must NOT invoke the runner");
    }

    [Fact]
    public async Task Handle_RunnerThrowsForOneRow_ContinuesBatchAndReportsFailed()
    {
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();

        _attempts.Setup(r => r.GetSharedGameIdsByAttemptIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, Guid> { [a1] = g1, [a2] = g2 });

        _runner.Setup(r => r.EnrichAndRecordAsync(g1, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated runner failure"));
        _runner.Setup(r => r.EnrichAndRecordAsync(g2, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("k", "CC0", null, "u"));

        var result = await Sut().Handle(
            new AdminBulkRetryWikidataCoverCommand(
                AttemptIds: new[] { a1, a2 },
                TriggeredByUserId: Guid.NewGuid()),
            default);

        result.TriggeredCount.Should().Be(1);
        result.NotFoundCount.Should().Be(0);
        result.FailedCount.Should().Be(1, "the per-row try/catch MUST not break the batch");
        result.Rows.Single(r => r.AttemptId == a1).Outcome.Should().Be(AdminBulkRetryRow.OutcomeFailed);
        result.Rows.Single(r => r.AttemptId == a1).Reason.Should().Be("InvalidOperationException");
        result.Rows.Single(r => r.AttemptId == a2).Outcome.Should().Be(AdminBulkRetryRow.OutcomeTriggered);
    }

    [Fact]
    public async Task Handle_CancellationRequested_PropagatesAndDoesNotSwallow()
    {
        var a1 = Guid.NewGuid();
        var g1 = Guid.NewGuid();

        _attempts.Setup(r => r.GetSharedGameIdsByAttemptIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, Guid> { [a1] = g1 });

        _runner.Setup(r => r.EnrichAndRecordAsync(g1, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = async () => await Sut().Handle(
            new AdminBulkRetryWikidataCoverCommand(
                AttemptIds: new[] { a1 },
                TriggeredByUserId: Guid.NewGuid()),
            default);

        await act.Should().ThrowAsync<OperationCanceledException>(
            "cancellation MUST propagate so partial batches aren't silently truncated");
    }

    [Fact]
    public async Task Handle_EmptyMap_ReturnsAllNotFound()
    {
        _attempts.Setup(r => r.GetSharedGameIdsByAttemptIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, Guid>());

        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        var result = await Sut().Handle(
            new AdminBulkRetryWikidataCoverCommand(
                AttemptIds: ids,
                TriggeredByUserId: Guid.NewGuid()),
            default);

        result.TriggeredCount.Should().Be(0);
        result.NotFoundCount.Should().Be(3);
        result.FailedCount.Should().Be(0);
        result.Rows.Should().HaveCount(3);
        _runner.Verify(r => r.EnrichAndRecordAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
