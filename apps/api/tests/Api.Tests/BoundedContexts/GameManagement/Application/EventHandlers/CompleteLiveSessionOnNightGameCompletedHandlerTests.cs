using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.EventHandlers;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// #4752 (the game night → live game bridge), night → live direction: completing the current game from
/// the night view completes the STARTED live game on the same tracking Session — so it does not stay
/// InProgress forever holding a quota slot and never producing its PlayRecord.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class CompleteLiveSessionOnNightGameCompletedHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _liveRepo = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly CompleteLiveSessionOnNightGameCompletedHandler _sut;

    public CompleteLiveSessionOnNightGameCompletedHandlerTests()
    {
        _sut = new CompleteLiveSessionOnNightGameCompletedHandler(
            _liveRepo.Object, _mediator.Object,
            NullLogger<CompleteLiveSessionOnNightGameCompletedHandler>.Instance);
    }

    private static GameCompletedInNightEvent EventFor(Guid trackingSessionId) =>
        new(GameNightId: Guid.NewGuid(), SessionId: trackingSessionId, GameId: Guid.NewGuid(),
            GameTitle: "Catan", WinnerId: null);

    private static LiveGameSession LiveIn(LiveSessionStatus status, Guid trackingSessionId)
    {
        var live = LiveGameSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", TimeProvider.System, Guid.NewGuid(),
            trackingSessionId: trackingSessionId);
        if (status == LiveSessionStatus.Created)
            return live;

        live.AddPlayer(Guid.NewGuid(), "Marta", PlayerColor.Red);
        live.Start();
        switch (status)
        {
            case LiveSessionStatus.Paused:
                live.Pause();
                break;
            case LiveSessionStatus.Completed:
                live.Complete();
                break;
        }

        return live;
    }

    private void ArrangeLive(Guid trackingSessionId, LiveGameSession? live) =>
        _liveRepo.Setup(r => r.GetByTrackingSessionIdAsync(trackingSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(live);

    [Theory]
    [InlineData(LiveSessionStatus.InProgress)]
    [InlineData(LiveSessionStatus.Paused)]
    public async Task Handle_AStartedLiveGame_IsCompleted(LiveSessionStatus status)
    {
        var tracking = Guid.NewGuid();
        var live = LiveIn(status, tracking);
        ArrangeLive(tracking, live);

        await _sut.Handle(EventFor(tracking), CancellationToken.None);

        _mediator.Verify(m => m.Send(
            It.Is<CompleteLiveSessionCommand>(c => c.SessionId == live.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(LiveSessionStatus.Created)]
    [InlineData(LiveSessionStatus.Completed)]
    public async Task Handle_ALiveGameNobodyStartedOrAlreadyCompleted_IsLeftAlone(LiveSessionStatus status)
    {
        // Created: nothing to record, and not "active" anywhere. Completed: the live → night direction
        // got there first — acting again would loop.
        var tracking = Guid.NewGuid();
        ArrangeLive(tracking, LiveIn(status, tracking));

        await _sut.Handle(EventFor(tracking), CancellationToken.None);

        _mediator.Verify(m => m.Send(It.IsAny<CompleteLiveSessionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ASittingWithNoLiveGame_IsLeftAlone()
    {
        // A night sitting that predates the bridge has no live game.
        var tracking = Guid.NewGuid();
        ArrangeLive(tracking, null);

        await _sut.Handle(EventFor(tracking), CancellationToken.None);

        _mediator.Verify(m => m.Send(It.IsAny<CompleteLiveSessionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCompletingTheLiveGameFails_TheErrorPropagatesSoTheOutboxRetries()
    {
        // The outbox retries a failed event, and a retry is harmless (it finds the game completed).
        var tracking = Guid.NewGuid();
        ArrangeLive(tracking, LiveIn(LiveSessionStatus.InProgress, tracking));
        _mediator.Setup(m => m.Send(It.IsAny<CompleteLiveSessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var act = () => _sut.Handle(EventFor(tracking), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
