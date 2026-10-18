using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
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
public sealed class UpdateMechanicClaimStructureCommandHandlerTests
{
    private readonly Mock<IMechanicAnalysisRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly UpdateMechanicClaimStructureCommandHandler _handler;

    public UpdateMechanicClaimStructureCommandHandlerTests()
    {
        _handler = new UpdateMechanicClaimStructureCommandHandler(
            _repo.Object, _uow.Object, Mock.Of<ILogger<UpdateMechanicClaimStructureCommandHandler>>());
    }

    private static MechanicAnalysis InReview(int claims)
    {
        var a = MechanicAnalysis.Create(Guid.NewGuid(), Guid.NewGuid(), "v1.2.0", Guid.NewGuid(), DateTime.UtcNow, "m", "p", 1m);
        for (var i = 0; i < claims; i++)
        {
            var cit = MechanicCitation.Create(Guid.NewGuid(), 1, "q", null, 0);
            a.AddClaim(MechanicClaim.Create(a.Id, MechanicSection.Mechanics, $"c{i}", i, new[] { cit }));
        }
        a.SubmitForReview(Guid.NewGuid(), DateTime.UtcNow);
        return a;
    }

    [Fact]
    public async Task Handle_SetsStructure_AndReturnsDtoWithFields()
    {
        var a = InReview(2);
        _repo.Setup(r => r.GetByIdWithClaimsIgnoringFiltersAsync(a.Id, It.IsAny<CancellationToken>())).ReturnsAsync(a);
        var dto = new MechanicClaimStructureDto(MechanicClaimKind.Exception, MechanicRulePriority.Card, new[] { a.Claims[0].Id }, new MechanicTriggerDto("Azione", null, null));

        var result = await _handler.Handle(new UpdateMechanicClaimStructureCommand(a.Id, a.Claims[1].Id, Guid.NewGuid(), dto), CancellationToken.None);

        result.Kind.Should().Be(MechanicClaimKind.Exception);
        result.Priority.Should().Be(MechanicRulePriority.Card);
        result.Overrides.Should().Equal(a.Claims[0].Id);
        result.Trigger!.Phase.Should().Be("azione");
        _repo.Verify(r => r.Update(a), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SelfOverride_ThrowsBadRequest()
    {
        var a = InReview(1);
        _repo.Setup(r => r.GetByIdWithClaimsIgnoringFiltersAsync(a.Id, It.IsAny<CancellationToken>())).ReturnsAsync(a);
        var self = new MechanicClaimStructureDto(MechanicClaimKind.Exception, MechanicRulePriority.Base, new[] { a.Claims[0].Id }, null);

        var act = () => _handler.Handle(new UpdateMechanicClaimStructureCommand(a.Id, a.Claims[0].Id, Guid.NewGuid(), self), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task Handle_UnknownClaim_404()
    {
        var a = InReview(1);
        _repo.Setup(r => r.GetByIdWithClaimsIgnoringFiltersAsync(a.Id, It.IsAny<CancellationToken>())).ReturnsAsync(a);
        var act = () => _handler.Handle(new UpdateMechanicClaimStructureCommand(a.Id, Guid.NewGuid(), Guid.NewGuid(), new MechanicClaimStructureDto(MechanicClaimKind.Rule, MechanicRulePriority.Base, Array.Empty<Guid>(), null)), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static readonly MechanicClaimStructureDto Clarification =
        new(MechanicClaimKind.Clarification, MechanicRulePriority.Base, Array.Empty<Guid>(), null);

    [Fact]
    public async Task Handle_PublishedAnalysis_ThrowsConflict()
    {
        // A Published analysis is frozen: its card is the validator contract; edits go through a new analysis.
        var a = InReview(1);
        a.ApproveClaim(a.Claims[0].Id, Guid.NewGuid(), DateTime.UtcNow);
        a.Approve(Guid.NewGuid(), DateTime.UtcNow);
        a.Status.Should().Be(MechanicAnalysisStatus.Published);
        _repo.Setup(r => r.GetByIdWithClaimsIgnoringFiltersAsync(a.Id, It.IsAny<CancellationToken>())).ReturnsAsync(a);

        var act = () => _handler.Handle(new UpdateMechanicClaimStructureCommand(a.Id, a.Claims[0].Id, Guid.NewGuid(), Clarification), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("L'analisi non è in revisione: la struttura non può essere modificata.");
        a.Claims[0].Kind.Should().Be(MechanicClaimKind.Rule, "nothing applied");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DraftAnalysis_ThrowsConflict()
    {
        var a = MechanicAnalysis.Create(Guid.NewGuid(), Guid.NewGuid(), "v1.2.0", Guid.NewGuid(), DateTime.UtcNow, "m", "p", 1m);
        var cit = MechanicCitation.Create(Guid.NewGuid(), 1, "q", null, 0);
        a.AddClaim(MechanicClaim.Create(a.Id, MechanicSection.Mechanics, "c0", 0, new[] { cit }));
        _repo.Setup(r => r.GetByIdWithClaimsIgnoringFiltersAsync(a.Id, It.IsAny<CancellationToken>())).ReturnsAsync(a);

        var act = () => _handler.Handle(new UpdateMechanicClaimStructureCommand(a.Id, a.Claims[0].Id, Guid.NewGuid(), Clarification), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*non è in revisione*");
    }

    [Theory]
    [InlineData(MechanicAnalysisStatus.Rejected)]
    [InlineData(MechanicAnalysisStatus.PartiallyExtracted)]
    public async Task Handle_AnalysisRejectedOrPartiallyExtracted_SetsStructure(MechanicAnalysisStatus status)
    {
        MechanicAnalysis a;
        if (status == MechanicAnalysisStatus.Rejected)
        {
            a = InReview(1);
            a.Reject(Guid.NewGuid(), "rivedere", DateTime.UtcNow);
        }
        else
        {
            a = MechanicAnalysis.Create(Guid.NewGuid(), Guid.NewGuid(), "v1.2.0", Guid.NewGuid(), DateTime.UtcNow, "m", "p", 1m);
            var cit = MechanicCitation.Create(Guid.NewGuid(), 1, "q", null, 0);
            a.AddClaim(MechanicClaim.Create(a.Id, MechanicSection.Mechanics, "c0", 0, new[] { cit }));
            a.MarkAsPartiallyExtracted("partial", Guid.NewGuid(), DateTime.UtcNow);
        }

        a.Status.Should().Be(status);
        _repo.Setup(r => r.GetByIdWithClaimsIgnoringFiltersAsync(a.Id, It.IsAny<CancellationToken>())).ReturnsAsync(a);

        var result = await _handler.Handle(new UpdateMechanicClaimStructureCommand(a.Id, a.Claims[0].Id, Guid.NewGuid(), Clarification), CancellationToken.None);

        result.Kind.Should().Be(MechanicClaimKind.Clarification);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RejectedClaim_ThrowsConflict()
    {
        var a = InReview(1);
        a.RejectClaim(a.Claims[0].Id, Guid.NewGuid(), "sbagliato", DateTime.UtcNow);
        _repo.Setup(r => r.GetByIdWithClaimsIgnoringFiltersAsync(a.Id, It.IsAny<CancellationToken>())).ReturnsAsync(a);

        var act = () => _handler.Handle(new UpdateMechanicClaimStructureCommand(a.Id, a.Claims[0].Id, Guid.NewGuid(), Clarification), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*Rejected*");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ConcurrencyFailure_ThrowsConflict()
    {
        var a = InReview(1);
        _repo.Setup(r => r.GetByIdWithClaimsIgnoringFiltersAsync(a.Id, It.IsAny<CancellationToken>())).ReturnsAsync(a);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateConcurrencyException());

        var act = () => _handler.Handle(new UpdateMechanicClaimStructureCommand(a.Id, a.Claims[0].Id, Guid.NewGuid(), Clarification), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*modified by another operation*");
    }
}
