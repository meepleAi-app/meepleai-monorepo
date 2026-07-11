using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
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
/// Guard tests for <see cref="PublishMechanicCardCommandHandler"/> (#2783 WS2). Promotes the two
/// publish preconditions (no claims / not all claims Approved) from factory-only checks to explicit
/// labeled handler guards. A <c>Published</c> analysis reached through <c>MechanicAnalysis.Approve</c>
/// can never violate these (Approve enforces them), so the fixtures deliberately construct the
/// invariant-unreachable state via <c>Reconstitute</c> to exercise the defense-in-depth guards.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "2783")]
public sealed class PublishMechanicCardCommandHandlerGuardsTests
{
    private readonly Mock<IMechanicAnalysisRepository> _analysisRepo = new();
    private readonly Mock<IMechanicCardRepository> _cardRepo = new();
    private readonly Mock<ISharedGameRepository> _gameRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private PublishMechanicCardCommandHandler CreateHandler() =>
        new(_analysisRepo.Object, _cardRepo.Object, _gameRepo.Object, _uow.Object,
            TimeProvider.System, NullLogger<PublishMechanicCardCommandHandler>.Instance);

    private void SetupAnalysis(MechanicAnalysis analysis) =>
        _analysisRepo
            .Setup(r => r.GetByIdWithClaimsIgnoringFiltersAsync(analysis.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);

    [Fact]
    public async Task Handle_PublishedAnalysisWithNoClaims_ThrowsConflict_BeforeFactory()
    {
        var analysis = BuildPublishedAnalysis(Array.Empty<MechanicClaimStatus>());
        SetupAnalysis(analysis);

        var act = () => CreateHandler().Handle(
            new PublishMechanicCardCommand(analysis.Id, "A valid title", null, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*no claims to publish*");
        _cardRepo.Verify(r => r.AddAsync(It.IsAny<MechanicCard>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PublishedAnalysisWithUnapprovedClaim_ThrowsConflict_BeforeFactory()
    {
        var analysis = BuildPublishedAnalysis(new[] { MechanicClaimStatus.Pending });
        SetupAnalysis(analysis);

        var act = () => CreateHandler().Handle(
            new PublishMechanicCardCommand(analysis.Id, "A valid title", null, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*must be Approved*");
        _cardRepo.Verify(r => r.AddAsync(It.IsAny<MechanicCard>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Builds a <c>Published</c> analysis with one claim per given status, bypassing
    /// <c>MechanicAnalysis.Approve</c>'s invariants via <c>Reconstitute</c> (the repository's rehydration
    /// path). An empty array yields a Published analysis with zero claims.
    /// </summary>
    private static MechanicAnalysis BuildPublishedAnalysis(MechanicClaimStatus[] claimStatuses)
    {
        var analysisId = Guid.NewGuid();
        var claims = new List<MechanicClaim>();
        for (var i = 0; i < claimStatuses.Length; i++)
        {
            var claimId = Guid.NewGuid();
            var citation = MechanicCitation.Reconstitute(
                id: Guid.NewGuid(),
                claimId: claimId,
                pdfPage: i + 1,
                quote: $"Quote {i}",
                chunkId: null,
                displayOrder: 0);
            claims.Add(MechanicClaim.Reconstitute(
                id: claimId,
                analysisId: analysisId,
                section: MechanicSection.Summary,
                text: $"Claim {i}",
                displayOrder: i,
                status: claimStatuses[i],
                reviewedBy: Guid.NewGuid(),
                reviewedAt: DateTime.UtcNow,
                rejectionNote: null,
                citations: new[] { citation }));
        }

        return MechanicAnalysis.Reconstitute(
            id: analysisId,
            sharedGameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            promptVersion: "v1",
            status: MechanicAnalysisStatus.Published,
            createdBy: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            reviewedBy: Guid.NewGuid(),
            reviewedAt: DateTime.UtcNow,
            rejectionReason: null,
            totalTokensUsed: 0,
            estimatedCostUsd: 0m,
            modelUsed: "gpt-4",
            provider: "openai",
            costCapUsd: 1m,
            costCapOverrideAt: null,
            costCapOverrideBy: null,
            costCapOverrideReason: null,
            isSuppressed: false,
            suppressedAt: null,
            suppressedBy: null,
            suppressionReason: null,
            suppressionRequestedAt: null,
            suppressionRequestSource: null,
            claims: claims);
    }
}
