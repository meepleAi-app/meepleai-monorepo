using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
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

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class ApproveMechanicClaimCommandHandlerStructureTests
{
    private readonly Mock<IMechanicAnalysisRepository> _repositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<ApproveMechanicClaimCommandHandler>> _loggerMock = new();
    private readonly ApproveMechanicClaimCommandHandler _handler;

    public ApproveMechanicClaimCommandHandlerStructureTests()
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
    public async Task Handle_WithStructure_AppliesItBeforeApproving()
    {
        var analysis = BuildInReviewAnalysis(2);
        var (general, exc) = (analysis.Claims[0], analysis.Claims[1]);
        SetupRepo(analysis, analysis.Id);
        var structure = new MechanicClaimStructureDto(MechanicClaimKind.Exception, MechanicRulePriority.Card, new[] { general.Id }, null);

        var result = await _handler.Handle(new ApproveMechanicClaimCommand(analysis.Id, exc.Id, Guid.NewGuid(), null, structure), CancellationToken.None);

        result.Status.Should().Be(MechanicClaimStatus.Approved);
        result.Kind.Should().Be(MechanicClaimKind.Exception);
        result.Overrides.Should().Equal(general.Id);
    }

    [Fact]
    public async Task Handle_WithoutStructure_LeavesProposedValues()
    {
        var analysis = BuildInReviewAnalysis(1);
        analysis.SetClaimStructure(analysis.Claims[0].Id, new MechanicClaimStructure(MechanicClaimKind.Clarification, MechanicRulePriority.Base, Array.Empty<Guid>(), null));
        SetupRepo(analysis, analysis.Id);

        var result = await _handler.Handle(new ApproveMechanicClaimCommand(analysis.Id, analysis.Claims[0].Id, Guid.NewGuid()), CancellationToken.None);

        result.Kind.Should().Be(MechanicClaimKind.Clarification);
    }
}
