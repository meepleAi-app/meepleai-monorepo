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
/// Spec 2026-10-08 §2 / ruling R10: the implicit approve-all sweep must not approve a claim whose
/// structure violates an override-graph invariant (parser-built claims bypass SetClaimStructure).
/// Such claims are skipped and stay Pending, like the fail-flagged ones.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class BulkApproveMechanicClaimsStructureTests
{
    private readonly Mock<IMechanicAnalysisRepository> _repositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly BulkApproveMechanicClaimsCommandHandler _handler;

    public BulkApproveMechanicClaimsStructureTests()
    {
        _handler = new BulkApproveMechanicClaimsCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            TimeProvider.System,
            Mock.Of<ILogger<BulkApproveMechanicClaimsCommandHandler>>());
    }

    private static MechanicClaim ParserClaim(Guid analysisId, int order, MechanicClaimStructure structure, Guid? id = null)
    {
        var claimId = id ?? Guid.NewGuid();
        var citation = MechanicCitation.Create(claimId, pdfPage: 1, quote: "q", chunkId: null, displayOrder: 0);
        return MechanicClaim.CreateWithId(
            claimId, analysisId, MechanicSection.Mechanics, $"claim {order}", order, new[] { citation },
            sourceAnchor: $"$.mechanics[{order}]", structure: structure);
    }

    [Fact]
    public async Task BulkApprove_SkipsClaimsWithInvalidStructure()
    {
        var analysis = MechanicAnalysis.Create(
            Guid.NewGuid(), Guid.NewGuid(), "v1.2.0", Guid.NewGuid(), DateTime.UtcNow, "m", "p", 1m);
        var example = ParserClaim(analysis.Id, 1,
            new MechanicClaimStructure(MechanicClaimKind.Example, MechanicRulePriority.Base, Array.Empty<Guid>(), null));
        var rule = ParserClaim(analysis.Id, 0, MechanicClaimStructure.Default);
        var exceptionToExample = ParserClaim(analysis.Id, 2,
            new MechanicClaimStructure(MechanicClaimKind.Exception, MechanicRulePriority.Card, new[] { example.Id }, null));
        var unboundException = ParserClaim(analysis.Id, 3,
            new MechanicClaimStructure(MechanicClaimKind.Exception, MechanicRulePriority.Card, Array.Empty<Guid>(), null));
        analysis.AddClaim(rule);
        analysis.AddClaim(example);
        analysis.AddClaim(exceptionToExample);
        analysis.AddClaim(unboundException);
        analysis.SubmitForReview(Guid.NewGuid(), DateTime.UtcNow);
        _repositoryMock
            .Setup(r => r.GetByIdWithClaimsIgnoringFiltersAsync(analysis.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);

        var result = await _handler.Handle(
            new BulkApproveMechanicClaimsCommand(analysis.Id, Guid.NewGuid()), CancellationToken.None);

        result.ApprovedCount.Should().Be(1);
        rule.Status.Should().Be(MechanicClaimStatus.Approved);
        example.Status.Should().Be(MechanicClaimStatus.Pending, "an Example targeted by an override is invalid");
        exceptionToExample.Status.Should().Be(MechanicClaimStatus.Pending, "an Exception overriding an Example is invalid");
        unboundException.Status.Should().Be(MechanicClaimStatus.Pending, "an Exception with no override and no trigger is invalid");
        result.Claims.Where(c => c.Status == MechanicClaimStatus.Pending).Should().HaveCount(3);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
