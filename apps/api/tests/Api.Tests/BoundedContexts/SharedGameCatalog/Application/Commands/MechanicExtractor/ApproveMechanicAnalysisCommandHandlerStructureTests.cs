using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Ruling R10: the publish transition (InReview → Published) refuses an analysis whose claims violate
/// an override-graph invariant, so an invalid graph never reaches the card (spec 2026-10-08 §6).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class ApproveMechanicAnalysisCommandHandlerStructureTests
{
    private readonly Mock<IMechanicAnalysisRepository> _repositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly ApproveMechanicAnalysisCommandHandler _handler;

    public ApproveMechanicAnalysisCommandHandlerStructureTests()
    {
        _handler = new ApproveMechanicAnalysisCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            TimeProvider.System,
            Mock.Of<ILogger<ApproveMechanicAnalysisCommandHandler>>());
    }

    [Fact]
    public async Task Handle_InvalidClaimGraph_Throws409_AndDoesNotSave()
    {
        var analysis = MechanicAnalysis.Create(
            Guid.NewGuid(), Guid.NewGuid(), "v1.2.0", Guid.NewGuid(), DateTime.UtcNow, "m", "p", 1m);
        var id = Guid.NewGuid();
        var citation = MechanicCitation.Create(id, pdfPage: 1, quote: "q", chunkId: null, displayOrder: 0);
        var unbound = MechanicClaim.CreateWithId(id, analysis.Id, MechanicSection.Mechanics, "eccezione", 0,
            new[] { citation }, sourceAnchor: "$.mechanics[0]",
            structure: new MechanicClaimStructure(MechanicClaimKind.Exception, MechanicRulePriority.Card, Array.Empty<Guid>(), null));
        analysis.AddClaim(unbound);
        analysis.SubmitForReview(Guid.NewGuid(), DateTime.UtcNow);
        unbound.Approve(Guid.NewGuid(), DateTime.UtcNow); // entity-level: bypasses the aggregate gate
        _repositoryMock
            .Setup(r => r.GetByIdWithClaimsIgnoringFiltersAsync(analysis.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);

        var act = () => _handler.Handle(new ApproveMechanicAnalysisCommand(analysis.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*claim {unbound.Id}*");
        analysis.Status.Should().Be(MechanicAnalysisStatus.InReview);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
