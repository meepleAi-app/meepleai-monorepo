using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// #4752 (the game night → live game bridge), live → night direction: when «Termina partita» completes
/// a live game that belongs to a night, the night's link is completed too — with the winner the live
/// scores decided, mapped to the tracking Session's participant. Never a guess, never a loop.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class CompleteNightLinkForLiveSessionCommandHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _liveRepo = new();
    private readonly Mock<IGameNightEventRepository> _nightRepo = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly CompleteNightLinkForLiveSessionCommandHandler _sut;

    public CompleteNightLinkForLiveSessionCommandHandlerTests()
    {
        _sut = new CompleteNightLinkForLiveSessionCommandHandler(
            _liveRepo.Object, _nightRepo.Object, _mediator.Object,
            NullLogger<CompleteNightLinkForLiveSessionCommandHandler>.Instance);
    }

    private static LiveGameSession Live(
        Guid trackingSessionId, bool complete, params (Guid? UserId, string Name, int? Score)[] seats)
    {
        var live = LiveGameSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", TimeProvider.System, Guid.NewGuid(),
            trackingSessionId: trackingSessionId == Guid.Empty ? null : trackingSessionId);
        var colors = Enum.GetValues<PlayerColor>();
        for (var i = 0; i < seats.Length; i++)
            live.AddPlayer(seats[i].UserId, seats[i].Name, colors[i]);

        live.Start();
        for (var i = 0; i < seats.Length; i++)
        {
            if (seats[i].Score is { } score)
                live.RecordScore(live.Players[i].Id, 1, "points", score);
        }

        if (complete)
            live.Complete();
        return live;
    }

    private static GameNightEvent NightWithInProgressLink(Guid trackingSessionId)
    {
        var night = GameNightEvent.Create(
            Guid.NewGuid(), "Serata", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        night.Publish([]);
        night.AddSession(trackingSessionId, night.GameIds[0], "Catan");
        night.StartSession(trackingSessionId);
        return night;
    }

    private void Arrange(LiveGameSession live, GameNightEvent? night, Guid trackingSessionId)
    {
        _liveRepo.Setup(r => r.GetByIdAsync(live.Id, It.IsAny<CancellationToken>())).ReturnsAsync(live);
        _nightRepo.Setup(r => r.GetByLinkedSessionIdAsync(trackingSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(night);
    }

    private void ArrangeTrackingParticipants(Guid trackingSessionId, params (Guid ParticipantId, Guid UserId)[] participants) =>
        _mediator.Setup(m => m.Send(
                It.Is<GetSessionDetailsQuery>(q => q.SessionId == trackingSessionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionDetailsDto
            {
                Participants = participants
                    .Select(p => new ParticipantDto { Id = p.ParticipantId, UserId = p.UserId, DisplayName = "x" })
                    .ToList(),
            });

    private Task Handle(Guid liveId) =>
        _sut.Handle(new CompleteNightLinkForLiveSessionCommand(liveId), CancellationToken.None);

    private void VerifyNightNotCompleted() =>
        _mediator.Verify(m => m.Send(It.IsAny<CompleteGameNightSessionCommand>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact]
    public async Task Handle_CompletesTheNightLink_WithTheTrackingParticipantOfTheFirstPlacePlayer()
    {
        var tracking = Guid.NewGuid();
        var marta = Guid.NewGuid();
        var luca = Guid.NewGuid();
        var lucaParticipant = Guid.NewGuid();
        var live = Live(tracking, complete: true, (marta, "Marta", 3), (luca, "Luca", 9));
        var night = NightWithInProgressLink(tracking);
        Arrange(live, night, tracking);
        ArrangeTrackingParticipants(tracking, (Guid.NewGuid(), marta), (lucaParticipant, luca));

        await Handle(live.Id);

        // The organizer is the caller the night's own completion authorizes.
        _mediator.Verify(m => m.Send(
            It.Is<CompleteGameNightSessionCommand>(c =>
                c.GameNightId == night.Id && c.WinnerId == lucaParticipant && c.UserId == night.OrganizerId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ATieForFirstPlace_CompletesWithoutAWinner()
    {
        var tracking = Guid.NewGuid();
        var marta = Guid.NewGuid();
        var luca = Guid.NewGuid();
        var live = Live(tracking, complete: true, (marta, "Marta", 5), (luca, "Luca", 5));
        var night = NightWithInProgressLink(tracking);
        Arrange(live, night, tracking);
        ArrangeTrackingParticipants(tracking, (Guid.NewGuid(), marta), (Guid.NewGuid(), luca));

        await Handle(live.Id);

        _mediator.Verify(m => m.Send(
            It.Is<CompleteGameNightSessionCommand>(c => c.WinnerId == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoScoresRecorded_CompletesWithoutAWinner()
    {
        var tracking = Guid.NewGuid();
        var marta = Guid.NewGuid();
        var live = Live(tracking, complete: true, (marta, "Marta", null), (Guid.NewGuid(), "Luca", null));
        var night = NightWithInProgressLink(tracking);
        Arrange(live, night, tracking);
        ArrangeTrackingParticipants(tracking, (Guid.NewGuid(), marta));

        await Handle(live.Id);

        _mediator.Verify(m => m.Send(
            It.Is<CompleteGameNightSessionCommand>(c => c.WinnerId == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AGuestInFirstPlace_CompletesWithoutAWinner()
    {
        // A night roster is Users only, but a host can seat a guest by name on the live page. A guest
        // has no tracking participant to map to: no winner, rather than a wrong one.
        var tracking = Guid.NewGuid();
        var live = Live(tracking, complete: true, (Guid.NewGuid(), "Marta", 3), ((Guid?)null, "Ospite", 9));
        var night = NightWithInProgressLink(tracking);
        Arrange(live, night, tracking);

        await Handle(live.Id);

        _mediator.Verify(m => m.Send(
            It.Is<CompleteGameNightSessionCommand>(c => c.WinnerId == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AWinnerWithNoTrackingParticipantOfTheirOwn_CompletesWithoutAWinner()
    {
        var tracking = Guid.NewGuid();
        var marta = Guid.NewGuid();
        var live = Live(tracking, complete: true, (marta, "Marta", 9), (Guid.NewGuid(), "Luca", 3));
        var night = NightWithInProgressLink(tracking);
        Arrange(live, night, tracking);
        ArrangeTrackingParticipants(tracking, (Guid.NewGuid(), Guid.NewGuid())); // nobody is Marta

        await Handle(live.Id);

        _mediator.Verify(m => m.Send(
            It.Is<CompleteGameNightSessionCommand>(c => c.WinnerId == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ALiveGameThatIsNotCompleted_DoesNothing()
    {
        var tracking = Guid.NewGuid();
        var live = Live(tracking, complete: false, (Guid.NewGuid(), "Marta", 3));
        Arrange(live, NightWithInProgressLink(tracking), tracking);

        await Handle(live.Id);

        VerifyNightNotCompleted();
    }

    [Fact]
    public async Task Handle_WhenTheNightLinkIsAlreadyCompleted_DoesNothing_SoTheTwoDirectionsCannotLoop()
    {
        var tracking = Guid.NewGuid();
        var live = Live(tracking, complete: true, (Guid.NewGuid(), "Marta", 3));
        var night = NightWithInProgressLink(tracking);
        night.CompleteCurrentSession(null);
        Arrange(live, night, tracking);

        await Handle(live.Id);

        VerifyNightNotCompleted();
    }

    [Fact]
    public async Task Handle_ALiveGameWithoutACompanion_DoesNothing()
    {
        var live = Live(Guid.Empty, complete: true, (Guid.NewGuid(), "Marta", 3));
        _liveRepo.Setup(r => r.GetByIdAsync(live.Id, It.IsAny<CancellationToken>())).ReturnsAsync(live);

        await Handle(live.Id);

        _nightRepo.Verify(r => r.GetByLinkedSessionIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNightNotCompleted();
    }

    [Fact]
    public async Task Handle_AWizardGameWhoseCompanionBelongsToNoNight_DoesNothing()
    {
        var tracking = Guid.NewGuid();
        var live = Live(tracking, complete: true, (Guid.NewGuid(), "Marta", 3));
        Arrange(live, night: null, tracking);

        await Handle(live.Id);

        VerifyNightNotCompleted();
    }

    [Fact]
    public async Task Handle_AUnknownLiveGame_DoesNothing()
    {
        _liveRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        await Handle(Guid.NewGuid());

        VerifyNightNotCompleted();
    }

    [Fact]
    public async Task Handle_WhenTheNightCompletionFails_TheErrorIsLoggedAndNotPropagated()
    {
        // The live game is already completed: «Termina partita» must not turn into an error because
        // the follow-up failed. The organizer can still complete the sitting from the night view.
        var tracking = Guid.NewGuid();
        var live = Live(tracking, complete: true, (Guid.NewGuid(), "Marta", 3));
        var night = NightWithInProgressLink(tracking);
        Arrange(live, night, tracking);
        _mediator.Setup(m => m.Send(It.IsAny<CompleteGameNightSessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("finalize failed"));

        var act = () => Handle(live.Id);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_WhenReadingTheLiveGameFails_TheErrorIsLoggedAndNotPropagated()
    {
        // The reads come BEFORE the follow-up, and used to sit outside the best-effort guard: on a repeated
        // «Termina partita» (the repair path, which answers 409) a failed read replaced that 409 with a 500,
        // and on the first one it turned the answer for a game that is over into an error.
        _liveRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("the database went away"));

        var act = () => Handle(Guid.NewGuid());

        await act.Should().NotThrowAsync();
        VerifyNightNotCompleted();
    }

    [Fact]
    public async Task Handle_WhenReadingTheNightFails_TheErrorIsLoggedAndNotPropagated()
    {
        var tracking = Guid.NewGuid();
        var live = Live(tracking, complete: true, (Guid.NewGuid(), "Marta", 3));
        _liveRepo.Setup(r => r.GetByIdAsync(live.Id, It.IsAny<CancellationToken>())).ReturnsAsync(live);
        _nightRepo.Setup(r => r.GetByLinkedSessionIdAsync(tracking, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("the database went away"));

        var act = () => Handle(live.Id);

        await act.Should().NotThrowAsync();
        VerifyNightNotCompleted();
    }

    [Fact]
    public async Task Handle_ACancelledRequest_StillPropagatesTheCancellation()
    {
        var tracking = Guid.NewGuid();
        var live = Live(tracking, complete: true, (Guid.NewGuid(), "Marta", 3));
        Arrange(live, NightWithInProgressLink(tracking), tracking);
        _mediator.Setup(m => m.Send(It.IsAny<CompleteGameNightSessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => Handle(live.Id);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
