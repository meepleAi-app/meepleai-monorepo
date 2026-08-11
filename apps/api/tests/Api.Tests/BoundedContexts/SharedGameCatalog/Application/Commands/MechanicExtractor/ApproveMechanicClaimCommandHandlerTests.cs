using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class ApproveMechanicClaimCommandHandlerTests
{
    private readonly Mock<IMechanicAnalysisRepository> _repositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<ApproveMechanicClaimCommandHandler>> _loggerMock = new();
    private readonly ApproveMechanicClaimCommandHandler _handler;

    public ApproveMechanicClaimCommandHandlerTests()
    {
        _handler = new ApproveMechanicClaimCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            TimeProvider.System,
            _loggerMock.Object);
    }

    // Builds an InReview analysis with `claimCount` Pending claims.
    private static MechanicAnalysis BuildInReviewAnalysis(int claimCount)
    {
        var analysis = MechanicAnalysis.Create(
            sharedGameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            promptVersion: "v1",
            createdBy: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            modelUsed: "test-model",
            provider: "test-provider",
            costCapUsd: 1.0m);

        for (var i = 0; i < claimCount; i++)
        {
            var citation = MechanicCitation.Create(Guid.NewGuid(), pdfPage: 1, quote: "q", chunkId: null, displayOrder: 0);
            var claim = MechanicClaim.Create(
                analysis.Id, MechanicSection.Mechanics, $"claim {i}", displayOrder: i, new[] { citation });
            analysis.AddClaim(claim);
        }

        analysis.SubmitForReview(Guid.NewGuid(), DateTime.UtcNow);
        return analysis;
    }

    private void SetupRepo(MechanicAnalysis? analysis, Guid analysisId)
    {
        _repositoryMock
            .Setup(r => r.GetByIdWithClaimsIgnoringFiltersAsync(analysisId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);
    }

    [Fact]
    public async Task Handle_WithNote_StoresReviewNote()
    {
        var analysis = BuildInReviewAnalysis(1);
        var claimId = analysis.Claims.Single().Id;
        SetupRepo(analysis, analysis.Id);

        var result = await _handler.Handle(
            new ApproveMechanicClaimCommand(analysis.Id, claimId, Guid.NewGuid(), "looks good, matches p.4"),
            CancellationToken.None);

        result.Status.Should().Be(MechanicClaimStatus.Approved);
        result.ReviewNote.Should().Be("looks good, matches p.4");
        _repositoryMock.Verify(r => r.Update(analysis), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutNote_LeavesReviewNoteNull()
    {
        var analysis = BuildInReviewAnalysis(1);
        var claimId = analysis.Claims.Single().Id;
        SetupRepo(analysis, analysis.Id);

        var result = await _handler.Handle(
            new ApproveMechanicClaimCommand(analysis.Id, claimId, Guid.NewGuid()),
            CancellationToken.None);

        result.Status.Should().Be(MechanicClaimStatus.Approved);
        result.ReviewNote.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AnalysisNotFound_Throws404()
    {
        var analysisId = Guid.NewGuid();
        SetupRepo(null, analysisId);

        var command = new ApproveMechanicClaimCommand(analysisId, Guid.NewGuid(), Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(command, CancellationToken.None));
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AnalysisNotInReview_Throws409()
    {
        // Draft analysis (never SubmitForReview) — ApproveClaim guard fails.
        var analysis = MechanicAnalysis.Create(
            Guid.NewGuid(), Guid.NewGuid(), "v1", Guid.NewGuid(), DateTime.UtcNow, "m", "p", 1.0m);
        var citation = MechanicCitation.Create(Guid.NewGuid(), 1, "q", null, 0);
        var claim = MechanicClaim.Create(analysis.Id, MechanicSection.Mechanics, "c", 0, new[] { citation });
        analysis.AddClaim(claim);
        SetupRepo(analysis, analysis.Id);

        var command = new ApproveMechanicClaimCommand(analysis.Id, claim.Id, Guid.NewGuid());

        await Assert.ThrowsAsync<ConflictException>(() => _handler.Handle(command, CancellationToken.None));
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ConcurrencyConflict_Throws409()
    {
        var analysis = BuildInReviewAnalysis(1);
        var claimId = analysis.Claims.Single().Id;
        SetupRepo(analysis, analysis.Id);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var command = new ApproveMechanicClaimCommand(analysis.Id, claimId, Guid.NewGuid());

        await Assert.ThrowsAsync<ConflictException>(() => _handler.Handle(command, CancellationToken.None));
    }
}
