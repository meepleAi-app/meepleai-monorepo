using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Covers;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Epic #3470 Slice 1c (write vertical) — the command that persists an admin's
/// per-context cover choice through the aggregate + reconcile path. Orchestration
/// is unit-tested here (mocked repo + unit of work over a REAL aggregate); the
/// actual Postgres persistence of the reconcile is covered by
/// <c>SharedGameCoverAssignmentReconcileIntegrationTests</c>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class AssignCoverCommandHandlerTests
{
    private static readonly Guid AdminId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private readonly Mock<ISharedGameRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private AssignCoverCommandHandler CreateAssignHandler() =>
        new(_repository.Object, _unitOfWork.Object, NullLogger<AssignCoverCommandHandler>.Instance);

    private RemoveCoverAssignmentCommandHandler CreateRemoveHandler() =>
        new(_repository.Object, _unitOfWork.Object, NullLogger<RemoveCoverAssignmentCommandHandler>.Instance);

    private static SharedGame NewGame() => SharedGame.Create(
        "Catan", 1995, "desc", 3, 4, 90, 10, 2.5m, 7.8m,
        "https://example.com/c.jpg", "https://example.com/c-thumb.jpg", null, AdminId);

    [Fact]
    public async Task Assign_MutatesAggregate_Reconciles_Saves_ReturnsDto()
    {
        var game = NewGame();
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        var handler = CreateAssignHandler();

        var dto = await handler.Handle(
            new AssignCoverCommand(game.Id, CoverContext.Card, CoverAssignmentSource.Wikidata, AdminId, 0.3, 0.7),
            CancellationToken.None);

        game.CoverAssignments.Should().ContainSingle();
        game.CoverAssignments.Single().Source.Should().Be(CoverAssignmentSource.Wikidata);

        _repository.Verify(r => r.ReconcileCoverAssignmentsAsync(game, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        dto.Context.Should().Be(CoverContext.Card);
        dto.Source.Should().Be(CoverAssignmentSource.Wikidata);
        dto.FocalX.Should().Be(0.3);
        dto.FocalY.Should().Be(0.7);
    }

    [Fact]
    public async Task Assign_GameNotFound_ThrowsNotFound_NoSave()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync((SharedGame?)null);
        var handler = CreateAssignHandler();

        var act = () => handler.Handle(
            new AssignCoverCommand(Guid.NewGuid(), CoverContext.Card, CoverAssignmentSource.Wikidata, AdminId),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Remove_ExistingAssignment_Reconciles_Saves()
    {
        var game = NewGame();
        game.AssignCover(CoverContext.Card, CoverAssignmentSource.Wikidata, AdminId);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        var handler = CreateRemoveHandler();

        await handler.Handle(new RemoveCoverAssignmentCommand(game.Id, CoverContext.Card, AdminId), CancellationToken.None);

        game.CoverAssignments.Should().BeEmpty();
        _repository.Verify(r => r.ReconcileCoverAssignmentsAsync(game, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Remove_GameNotFound_ThrowsNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync((SharedGame?)null);
        var handler = CreateRemoveHandler();

        var act = () => handler.Handle(
            new RemoveCoverAssignmentCommand(Guid.NewGuid(), CoverContext.Card, AdminId),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
