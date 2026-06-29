using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.GameNight;

/// <summary>
/// Cross-session authorization tests for the dispute sub-resource handlers (#2573 review finding).
/// The endpoint filter gates on the route {sessionId}; these handlers must additionally verify that
/// the dispute (loaded by {disputeId}) actually belongs to that session — otherwise a participant of
/// session A could mutate a dispute belonging to session B. The mismatch yields 404 (not 403) to
/// avoid leaking the existence of disputes in other sessions.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class DisputeCrossSessionAuthzTests
{
    private readonly Mock<IRuleDisputeRepository> _disputeRepo = new();

    private static RuleDispute OpenDisputeInSession(Guid sessionId)
        => RuleDispute.Open(sessionId, Guid.NewGuid(), Guid.NewGuid(), "Initiator claim");

    private void SetupGetById(RuleDispute dispute)
        => _disputeRepo.Setup(r => r.GetByIdAsync(dispute.Id, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(dispute);

    [Fact]
    public async Task RespondToDispute_DisputeBelongsToDifferentSession_ThrowsNotFound()
    {
        var dispute = OpenDisputeInSession(Guid.NewGuid());
        SetupGetById(dispute);
        var handler = new RespondToDisputeCommandHandler(_disputeRepo.Object, Mock.Of<IUnitOfWork>());

        var command = new RespondToDisputeCommand(
            SessionId: Guid.NewGuid(), DisputeId: dispute.Id,
            RespondentPlayerId: Guid.NewGuid(), RespondentClaim: "counter");

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RespondentTimeout_DisputeBelongsToDifferentSession_ThrowsNotFound()
    {
        var dispute = OpenDisputeInSession(Guid.NewGuid());
        SetupGetById(dispute);
        var handler = new RespondentTimeoutCommandHandler(_disputeRepo.Object);

        var command = new RespondentTimeoutCommand(SessionId: Guid.NewGuid(), DisputeId: dispute.Id);

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CastVote_DisputeBelongsToDifferentSession_ThrowsNotFound()
    {
        var dispute = OpenDisputeInSession(Guid.NewGuid());
        SetupGetById(dispute);
        var flag = new Mock<IFeatureFlagService>();
        flag.Setup(x => x.IsEnabledAsync("Features:Arbitro.DemocraticOverride", null)).ReturnsAsync(true);
        var handler = new CastVoteOnDisputeCommandHandler(_disputeRepo.Object, flag.Object, Mock.Of<IUnitOfWork>());

        var command = new CastVoteOnDisputeCommand(
            SessionId: Guid.NewGuid(), DisputeId: dispute.Id,
            PlayerId: Guid.NewGuid(), AcceptsVerdict: true);

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task TallyVotes_DisputeBelongsToDifferentSession_ThrowsNotFound()
    {
        var dispute = OpenDisputeInSession(Guid.NewGuid());
        SetupGetById(dispute);
        var handler = new TallyDisputeVotesCommandHandler(
            _disputeRepo.Object, Mock.Of<ILiveSessionRepository>(), Mock.Of<IUnitOfWork>());

        var command = new TallyDisputeVotesCommand(
            SessionId: Guid.NewGuid(), DisputeId: dispute.Id);

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
