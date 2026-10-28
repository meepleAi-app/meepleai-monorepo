using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// #4503: il comando per id che chiude un <c>Draft</c> la cui pipeline e' morta.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class ExpireStaleMechanicAnalysisDraftCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IMechanicAnalysisRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _time = new(Now);

    private ExpireStaleMechanicAnalysisDraftCommandHandler CreateHandler() =>
        new(_repository.Object, _unitOfWork.Object, _time);

    private static MechanicAnalysis NewDraft(Guid createdBy) =>
        MechanicAnalysis.Create(
            sharedGameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            promptVersion: "v1",
            createdBy: createdBy,
            createdAt: Now.UtcDateTime.AddHours(-3),
            modelUsed: "test-model",
            provider: "test-provider",
            costCapUsd: 1.00m);

    private void Loads(MechanicAnalysis? analysis) =>
        _repository
            .Setup(r => r.GetByIdWithClaimsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);

    [Fact]
    public async Task Handle_RejectsTheDraft_WithTheStaleReasonAndTheCreatingAdmin_AndSavesOnce()
    {
        var admin = Guid.NewGuid();
        var draft = NewDraft(admin);
        Loads(draft);

        await CreateHandler().Handle(new ExpireStaleMechanicAnalysisDraftCommand(draft.Id), CancellationToken.None);

        draft.Status.Should().Be(MechanicAnalysisStatus.Rejected);
        draft.RejectionReason.Should().Be(MechanicAnalysis.AutoRejectionReasons.StaleDraftExpired);
        draft.RejectionReason.Should().Be("stale_draft_expired");
        draft.ReviewedBy.Should().Be(admin);
        draft.ReviewedAt.Should().Be(Now.UtcDateTime);
        _repository.Verify(r => r.Update(draft), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheRowIsGone_Answers404_AndSavesNothing()
    {
        Loads(null);

        var act = () => CreateHandler()
            .Handle(new ExpireStaleMechanicAnalysisDraftCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenThePipelineClosedTheRowItself_Answers409_AndKeepsItsReason()
    {
        // Una pipeline lenta, non morta: tra l'elenco e questo comando ha chiuso da sé la riga. Non è un orfano,
        // e il motivo vero del rifiuto non va sovrascritto.
        var draft = NewDraft(Guid.NewGuid());
        draft.AutoRejectFromDraft(
            MechanicAnalysis.AutoRejectionReasons.LlmGenerationFailed,
            draft.CreatedBy,
            Now.UtcDateTime);
        Loads(draft);

        var act = () => CreateHandler()
            .Handle(new ExpireStaleMechanicAnalysisDraftCommand(draft.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        draft.RejectionReason.Should().Be(MechanicAnalysis.AutoRejectionReasons.LlmGenerationFailed);
        _repository.Verify(r => r.Update(It.IsAny<MechanicAnalysis>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
