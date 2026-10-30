using Api.Tests.TestHelpers;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// #4752 (the game night → live game bridge), go-live path: promoting a draft also gives it its live
/// game, from the draft's tracking Session and participants, through the one creation funnel. The
/// command is idempotent by the tracking Session id, so the #3218 self-heal retry never mints a second.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GoLiveSessionLiveGameBridgeTests
{
    private readonly Mock<IGameNightEventRepository> _repository = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly GoLiveSessionCommandHandler _handler;

    public GoLiveSessionLiveGameBridgeTests()
    {
        _unitOfWork.SetupExecuteInTransaction<StartGameNightSessionResult>();
        _handler = new GoLiveSessionCommandHandler(_repository.Object, _mediator.Object, _unitOfWork.Object);
    }

    private static GameNightEvent NightWithDraft(Guid sessionId, out Guid gameId)
    {
        var night = GameNightEvent.Create(
            Guid.NewGuid(), "Friday Night", DateTimeOffset.UtcNow.AddHours(1),
            gameIds: [Guid.NewGuid(), Guid.NewGuid()]);
        night.Publish([]);
        gameId = night.GameIds[0];
        night.AddSession(sessionId, gameId, "Catan");
        return night;
    }

    private static SessionDetailsDto DetailsWith(params ParticipantDto[] participants) =>
        new() { Participants = [.. participants] };

    [Fact]
    public async Task Handle_CreatesTheLiveGameFromTheDraftsTrackingSessionAndParticipants()
    {
        var sessionId = Guid.NewGuid();
        var night = NightWithDraft(sessionId, out var gameId);
        var owner = night.OrganizerId;
        var guest = Guid.NewGuid();
        var liveSessionId = Guid.NewGuid();

        _repository.Setup(r => r.GetByLinkedSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(night);
        // Stored out of order AND with the owner's join order HIGHER than the guest's: the owner must
        // still sit first on its own merit (the first seat is the Host), not by coincidence with the
        // join order.
        _mediator.Setup(m => m.Send(It.Is<GetSessionDetailsQuery>(q => q.SessionId == sessionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DetailsWith(
                new ParticipantDto { Id = Guid.NewGuid(), UserId = guest, DisplayName = "Luca", IsOwner = false, JoinOrder = 0 },
                new ParticipantDto { Id = Guid.NewGuid(), UserId = owner, DisplayName = "Marta", IsOwner = true, JoinOrder = 4 }));
        _mediator.Setup(m => m.Send(It.IsAny<CreateLiveSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(liveSessionId);

        var result = await _handler.Handle(new GoLiveSessionCommand(sessionId, owner), CancellationToken.None);

        result.LiveSessionId.Should().Be(liveSessionId);
        _mediator.Verify(m => m.Send(
            It.Is<CreateLiveSessionCommand>(c =>
                c.UserId == owner
                && c.GameName == "Catan"
                && c.GameId == gameId
                && c.ExistingTrackingSessionId == sessionId
                && c.InitialPlayers != null
                && c.InitialPlayers.Select(p => p.DisplayName).SequenceEqual(new[] { "Marta", "Luca" })
                && c.InitialPlayers[0].UserId == owner),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TheLiveGameIsCreatedBeforeLiveModeOpens()
    {
        // Live mode opens LAST (invariante #15). If the live game fails, the link is already
        // InProgress and the retry takes the self-heal branch, which comes back to create it.
        var sessionId = Guid.NewGuid();
        var night = NightWithDraft(sessionId, out _);
        var calls = new List<string>();
        _repository.Setup(r => r.GetByLinkedSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(night);
        _mediator.Setup(m => m.Send(It.IsAny<CreateLiveSessionCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("live"))
            .ReturnsAsync(Guid.NewGuid());
        _mediator.Setup(m => m.Send(It.IsAny<OpenSessionLiveModeCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("open-live-mode"))
            .Returns(Task.CompletedTask);

        await _handler.Handle(new GoLiveSessionCommand(sessionId, night.OrganizerId), CancellationToken.None);

        calls.Should().Equal("live", "open-live-mode");
    }

    [Fact]
    public async Task Handle_SelfHeal_AnInProgressLinkStillEnsuresTheLiveGame()
    {
        // A prior go-live promoted the link but failed before the live game / live mode: the retry
        // must skip the promotion and still end with a live game (idempotent by the tracking id).
        var sessionId = Guid.NewGuid();
        var night = NightWithDraft(sessionId, out _);
        night.StartSession(sessionId); // link already InProgress
        var liveSessionId = Guid.NewGuid();
        _repository.Setup(r => r.GetByLinkedSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(night);
        _mediator.Setup(m => m.Send(It.IsAny<CreateLiveSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(liveSessionId);

        var result = await _handler.Handle(new GoLiveSessionCommand(sessionId, night.OrganizerId), CancellationToken.None);

        result.LiveSessionId.Should().Be(liveSessionId);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<GameNightEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(
            It.Is<CreateLiveSessionCommand>(c => c.ExistingTrackingSessionId == sessionId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ADraftWithoutParticipants_StillGetsALiveGame()
    {
        // The roster is best-effort: with none, the live game starts empty and the host seats players
        // from the live page — going live must not fail for that.
        var sessionId = Guid.NewGuid();
        var night = NightWithDraft(sessionId, out _);
        _repository.Setup(r => r.GetByLinkedSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(night);
        _mediator.Setup(m => m.Send(It.IsAny<GetSessionDetailsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DetailsWith());
        _mediator.Setup(m => m.Send(It.IsAny<CreateLiveSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        await _handler.Handle(new GoLiveSessionCommand(sessionId, night.OrganizerId), CancellationToken.None);

        _mediator.Verify(m => m.Send(
            It.Is<CreateLiveSessionCommand>(c => c.InitialPlayers != null && c.InitialPlayers.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AnInvalidOperationFromTheWrite_IsNotAConflictAndDoesNotLeakItsMessage()
    {
        // The promotion catch used to wrap the whole write: an EF/Npgsql InvalidOperationException
        // became ConflictException(ex.Message) and reached the player. Only the aggregate's own guard
        // is a conflict.
        var sessionId = Guid.NewGuid();
        var night = NightWithDraft(sessionId, out _);
        _repository.Setup(r => r.GetByLinkedSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(night);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<GameNightEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("A second operation was started on this context"));

        var act = () => _handler.Handle(new GoLiveSessionCommand(sessionId, night.OrganizerId), CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Should().NotBeOfType<ConflictException>();
        _mediator.Verify(m => m.Send(It.IsAny<CreateLiveSessionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ACompletedLink_IsAConflictAndCreatesNoLiveGame()
    {
        // A finished sitting cannot go live (the handler's terminal-status branch), and must not get a
        // live game minted on the way.
        var sessionId = Guid.NewGuid();
        var night = NightWithDraft(sessionId, out _);
        night.StartSession(sessionId);
        night.CompleteCurrentSession(null);
        _repository.Setup(r => r.GetByLinkedSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(night);

        var act = () => _handler.Handle(new GoLiveSessionCommand(sessionId, night.OrganizerId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _mediator.Verify(m => m.Send(It.IsAny<CreateLiveSessionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
