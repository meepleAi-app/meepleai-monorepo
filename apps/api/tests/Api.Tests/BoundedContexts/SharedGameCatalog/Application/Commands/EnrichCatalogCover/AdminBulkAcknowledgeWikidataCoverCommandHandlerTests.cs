using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;

/// <summary>
/// Unit tests for <see cref="AdminBulkAcknowledgeWikidataCoverCommandHandler"/>.
/// Issue #1823 Phase F F5 (sub-issue #2254).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "2254")]
public class AdminBulkAcknowledgeWikidataCoverCommandHandlerTests
{
    private readonly Mock<IWikidataCoverEnrichmentAttemptRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly TimeProvider _time = TimeProvider.System;
    private readonly Guid _user = Guid.NewGuid();

    private AdminBulkAcknowledgeWikidataCoverCommandHandler Build() =>
        new(_repo.Object, _uow.Object, _time, NullLogger<AdminBulkAcknowledgeWikidataCoverCommandHandler>.Instance);

    private static WikidataCoverEnrichmentAttempt MakeDeadLetter(Guid? id = null) =>
        WikidataCoverEnrichmentAttempt.Reconstitute(
            id: id ?? Guid.NewGuid(),
            sharedGameId: Guid.NewGuid(),
            attemptedAt: DateTime.UtcNow,
            outcome: WikidataCoverEnrichmentOutcome.DeadLetter,
            reason: "r2-upload-error",
            details: null,
            retryCount: 3,
            nextRetryAt: null,
            deadLetteredAt: DateTime.UtcNow,
            acknowledgedAt: null,
            acknowledgedBy: null,
            triggeredByAdminUserId: null);

    [Fact]
    public async Task SingleDeadLetter_Acked_ReturnsAckedCountOne()
    {
        var dl = MakeDeadLetter();
        _repo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, WikidataCoverEnrichmentAttempt> { [dl.Id] = dl });

        var result = await Build().Handle(new AdminBulkAcknowledgeWikidataCoverCommand(
            AttemptIds: new[] { dl.Id },
            Note: null,
            TriggeredByUserId: _user), CancellationToken.None);

        result.AckedCount.Should().Be(1);
        result.IdempotentNoOpCount.Should().Be(0);
        result.NotFoundCount.Should().Be(0);
        result.Rows.Should().ContainSingle(r =>
            r.AttemptId == dl.Id && r.Outcome == AdminBulkAcknowledgeRow.OutcomeAcked);
        dl.AcknowledgedAt.Should().NotBeNull();
        dl.AcknowledgedBy.Should().Be(_user);
        _repo.Verify(r => r.UpdateAsync(dl, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AlreadyAcked_Idempotent_NotPersistedAgain()
    {
        var dl = MakeDeadLetter();
        dl.Acknowledge(Guid.NewGuid(), DateTime.UtcNow);
        _repo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, WikidataCoverEnrichmentAttempt> { [dl.Id] = dl });

        var result = await Build().Handle(new AdminBulkAcknowledgeWikidataCoverCommand(
            AttemptIds: new[] { dl.Id },
            Note: null,
            TriggeredByUserId: _user), CancellationToken.None);

        result.AckedCount.Should().Be(0);
        result.IdempotentNoOpCount.Should().Be(1);
        result.Rows.Single().Outcome.Should().Be(AdminBulkAcknowledgeRow.OutcomeAlreadyAcked);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<WikidataCoverEnrichmentAttempt>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never,
            "no mutation occurred so SaveChanges must NOT be invoked");
    }

    [Fact]
    public async Task NotFoundId_ReportedAsNotFound()
    {
        var missing = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, WikidataCoverEnrichmentAttempt>());

        var result = await Build().Handle(new AdminBulkAcknowledgeWikidataCoverCommand(
            AttemptIds: new[] { missing },
            Note: null,
            TriggeredByUserId: _user), CancellationToken.None);

        result.NotFoundCount.Should().Be(1);
        result.Rows.Single().Outcome.Should().Be(AdminBulkAcknowledgeRow.OutcomeNotFound);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<WikidataCoverEnrichmentAttempt>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NonDeadLetterRow_ReportedAsWrongState()
    {
        var success = WikidataCoverEnrichmentAttempt.Reconstitute(
            id: Guid.NewGuid(),
            sharedGameId: Guid.NewGuid(),
            attemptedAt: DateTime.UtcNow,
            outcome: WikidataCoverEnrichmentOutcome.Success,
            reason: "success",
            details: null,
            retryCount: 0,
            nextRetryAt: null,
            deadLetteredAt: null,
            acknowledgedAt: null,
            acknowledgedBy: null,
            triggeredByAdminUserId: null);
        _repo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, WikidataCoverEnrichmentAttempt> { [success.Id] = success });

        var result = await Build().Handle(new AdminBulkAcknowledgeWikidataCoverCommand(
            AttemptIds: new[] { success.Id },
            Note: null,
            TriggeredByUserId: _user), CancellationToken.None);

        result.AckedCount.Should().Be(0);
        result.WrongStateCount.Should().Be(1);
        result.Rows.Single().Outcome.Should().Be(AdminBulkAcknowledgeRow.OutcomeWrongState);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<WikidataCoverEnrichmentAttempt>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BatchMixedOutcomes_CountsAreAggregated()
    {
        var dl1 = MakeDeadLetter();
        var dl2Already = MakeDeadLetter();
        dl2Already.Acknowledge(Guid.NewGuid(), DateTime.UtcNow);
        var missing = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, WikidataCoverEnrichmentAttempt>
            {
                [dl1.Id] = dl1,
                [dl2Already.Id] = dl2Already,
            });

        var result = await Build().Handle(new AdminBulkAcknowledgeWikidataCoverCommand(
            AttemptIds: new[] { dl1.Id, dl2Already.Id, missing },
            Note: "operator note",
            TriggeredByUserId: _user), CancellationToken.None);

        result.AckedCount.Should().Be(1);
        result.IdempotentNoOpCount.Should().Be(1);
        result.NotFoundCount.Should().Be(1);
        result.WrongStateCount.Should().Be(0);
        result.Rows.Should().HaveCount(3);
        // SaveChanges invoked exactly once for the batch because exactly one row was mutated.
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancellationToken_Propagated()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _repo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var act = () => Build().Handle(new AdminBulkAcknowledgeWikidataCoverCommand(
            AttemptIds: new[] { Guid.NewGuid() },
            Note: null,
            TriggeredByUserId: _user), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
