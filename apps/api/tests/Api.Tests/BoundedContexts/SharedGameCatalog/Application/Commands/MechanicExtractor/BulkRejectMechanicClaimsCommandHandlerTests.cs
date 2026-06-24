using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class BulkRejectMechanicClaimsCommandHandlerTests
{
    private readonly Mock<IMechanicAnalysisRepository> _repositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<BulkRejectMechanicClaimsCommandHandler>> _loggerMock = new();
    private readonly BulkRejectMechanicClaimsCommandHandler _handler;

    public BulkRejectMechanicClaimsCommandHandlerTests()
    {
        _handler = new BulkRejectMechanicClaimsCommandHandler(
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
    public async Task Handle_RejectsRequestedClaims_AndPersists()
    {
        var analysis = BuildInReviewAnalysis(3);
        var ids = analysis.Claims.Select(c => c.Id).ToList();
        SetupRepo(analysis, analysis.Id);

        var command = new BulkRejectMechanicClaimsCommand(
            analysis.Id, new[] { ids[0], ids[1] }, "verbatim copy", Guid.NewGuid());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.RejectedCount.Should().Be(2);
        result.SkippedAlreadyRejectedCount.Should().Be(0);
        analysis.Claims.Count(c => c.Status == MechanicClaimStatus.Rejected).Should().Be(2);
        analysis.Claims.Single(c => c.Id == ids[2]).Status.Should().Be(MechanicClaimStatus.Pending);
        _repositoryMock.Verify(r => r.Update(analysis), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AnalysisNotFound_Throws404()
    {
        var analysisId = Guid.NewGuid();
        SetupRepo(null, analysisId);

        var command = new BulkRejectMechanicClaimsCommand(
            analysisId, new[] { Guid.NewGuid() }, "reason", Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(command, CancellationToken.None));
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MissingClaimId_Throws404_AndDoesNotPersist()
    {
        var analysis = BuildInReviewAnalysis(2);
        var ids = analysis.Claims.Select(c => c.Id).ToList();
        SetupRepo(analysis, analysis.Id);

        // One valid id + one stale id that does not belong to the analysis.
        var command = new BulkRejectMechanicClaimsCommand(
            analysis.Id, new[] { ids[0], Guid.NewGuid() }, "reason", Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => _handler.Handle(command, CancellationToken.None));
        analysis.Claims.Should().OnlyContain(c => c.Status == MechanicClaimStatus.Pending);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AlreadyRejectedClaim_IsSkippedIdempotently()
    {
        var analysis = BuildInReviewAnalysis(3);
        var ids = analysis.Claims.Select(c => c.Id).ToList();
        // Pre-reject the first claim.
        analysis.RejectClaim(ids[0], Guid.NewGuid(), "earlier note", DateTime.UtcNow);
        SetupRepo(analysis, analysis.Id);

        var command = new BulkRejectMechanicClaimsCommand(
            analysis.Id, new[] { ids[0], ids[1] }, "reason", Guid.NewGuid());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.RejectedCount.Should().Be(1);              // only ids[1] transitioned
        result.SkippedAlreadyRejectedCount.Should().Be(1); // ids[0] was already Rejected
        analysis.Claims.Count(c => c.Status == MechanicClaimStatus.Rejected).Should().Be(2);
    }

    [Fact]
    public async Task Handle_AnalysisNotInReview_Throws409()
    {
        // Draft analysis (never SubmitForReview) — RejectClaim guard fails.
        var analysis = MechanicAnalysis.Create(
            Guid.NewGuid(), Guid.NewGuid(), "v1", Guid.NewGuid(), DateTime.UtcNow, "m", "p", 1.0m);
        var citation = MechanicCitation.Create(Guid.NewGuid(), 1, "q", null, 0);
        var claim = MechanicClaim.Create(analysis.Id, MechanicSection.Mechanics, "c", 0, new[] { citation });
        analysis.AddClaim(claim);
        SetupRepo(analysis, analysis.Id);

        var command = new BulkRejectMechanicClaimsCommand(
            analysis.Id, new[] { claim.Id }, "reason", Guid.NewGuid());

        await Assert.ThrowsAsync<ConflictException>(() => _handler.Handle(command, CancellationToken.None));
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
