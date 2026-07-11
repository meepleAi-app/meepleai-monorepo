using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Smoke tests for the renamed Variant-C <see cref="FinalizeMechanicDraftCommandHandler"/> (#2783 WS1).
/// The command finalizes a <see cref="MechanicDraft"/> into a RulebookAnalysis; these lock the two guard
/// paths (draft-not-found → 404, already-finalized → 409) that had no coverage before the rename.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "2783")]
public sealed class FinalizeMechanicDraftCommandHandlerTests
{
    private readonly Mock<IMechanicDraftRepository> _draftRepo = new();
    private readonly Mock<IRulebookAnalysisRepository> _analysisRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private FinalizeMechanicDraftCommandHandler CreateHandler() =>
        new(_draftRepo.Object, _analysisRepo.Object, _uow.Object,
            NullLogger<FinalizeMechanicDraftCommandHandler>.Instance);

    [Fact]
    public async Task Handle_DraftNotFound_ThrowsNotFound()
    {
        var draftId = Guid.NewGuid();
        _draftRepo.Setup(r => r.GetByIdAsync(draftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MechanicDraft?)null);

        var act = () => CreateHandler().Handle(
            new FinalizeMechanicDraftCommand(draftId, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AlreadyFinalized_ThrowsConflict()
    {
        // MarkActivated has no precondition; drive the draft straight to Activated so the
        // idempotency guard (draft already finalized) trips before any content parsing.
        var draft = MechanicDraft.Create(Guid.NewGuid(), Guid.NewGuid(), "Catan", Guid.NewGuid());
        draft.MarkActivated();
        _draftRepo.Setup(r => r.GetByIdAsync(draft.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);

        var act = () => CreateHandler().Handle(
            new FinalizeMechanicDraftCommand(draft.Id, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already been finalized*");
    }
}
