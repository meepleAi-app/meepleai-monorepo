using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// D8 safety guard (#2782 FU-1): the implicit approve-all-pending path of
/// <see cref="BulkApproveMechanicClaimsCommandHandler"/> must not rubber-stamp claims carrying a
/// real <c>fail</c> validation — those require explicit per-claim review, not the bulk sweep.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class BulkApproveExcludeFailFlaggedTests
{
    private readonly Mock<IMechanicAnalysisRepository> _repositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<BulkApproveMechanicClaimsCommandHandler>> _loggerMock = new();
    private readonly BulkApproveMechanicClaimsCommandHandler _handler;

    public BulkApproveExcludeFailFlaggedTests()
    {
        _handler = new BulkApproveMechanicClaimsCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            TimeProvider.System,
            _loggerMock.Object);
    }

    // Builds an InReview analysis with 2 Pending claims: claim[0] clean, claim[1] carries a T2 fail.
    private static MechanicAnalysis BuildInReviewAnalysisWithTwoPendingClaims()
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

        var citation0 = MechanicCitation.Create(Guid.NewGuid(), pdfPage: 1, quote: "q0", chunkId: null, displayOrder: 0);
        var claim0 = MechanicClaim.Create(
            analysis.Id, MechanicSection.Mechanics, "claim 0 clean", displayOrder: 0, new[] { citation0 });
        analysis.AddClaim(claim0);

        var citation1 = MechanicCitation.Create(Guid.NewGuid(), pdfPage: 1, quote: "q1", chunkId: null, displayOrder: 0);
        var claim1 = MechanicClaim.Create(
            analysis.Id, MechanicSection.Mechanics, "claim 1 fail-flagged", displayOrder: 1, new[] { citation1 });
        claim1.AttachValidations(new[]
        {
            new MechanicClaimValidation("T2", MechanicClaimValidationOutcomes.Fail, "long verbatim", null),
        });
        analysis.AddClaim(claim1);

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
    public async Task Handle_ExcludesFailFlaggedClaims_FromImplicitApproveAll()
    {
        var analysis = BuildInReviewAnalysisWithTwoPendingClaims();
        SetupRepo(analysis, analysis.Id);

        var result = await _handler.Handle(
            new BulkApproveMechanicClaimsCommand(analysis.Id, Guid.NewGuid()), CancellationToken.None);

        analysis.Claims.ElementAt(0).Status.Should().Be(MechanicClaimStatus.Approved);
        analysis.Claims.ElementAt(1).Status.Should().Be(MechanicClaimStatus.Pending); // fail-flagged, skipped
        result.ApprovedCount.Should().Be(1);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
