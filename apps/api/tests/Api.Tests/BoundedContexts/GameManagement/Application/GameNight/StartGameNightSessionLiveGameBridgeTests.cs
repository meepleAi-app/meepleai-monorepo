using Api.Tests.TestHelpers;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Exceptions;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// #4752 (the game night → live game bridge): starting a game from a night also creates the
/// <c>LiveGameSession</c> — where the table plays — in the same transaction, from the one creation
/// funnel, adopting the tracking Session the night links to and seating the night's roster. Also pins
/// the error mapping the start used to get wrong: only the conflicts written for the player stay a
/// 409 with their message; anything else (EF, Npgsql) is not a conflict and must not leak.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class StartGameNightSessionLiveGameBridgeTests
{
    private readonly Mock<IGameNightEventRepository> _repository = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IAutoSaveSchedulerService> _autoSave = new();
    private readonly StartGameNightSessionCommandHandler _handler;

    public StartGameNightSessionLiveGameBridgeTests()
    {
        _unitOfWork.SetupExecuteInTransaction<StartGameNightSessionResult>();
        _handler = new StartGameNightSessionCommandHandler(
            _repository.Object, _mediator.Object, _unitOfWork.Object, _autoSave.Object);
    }

    private static UserDto UserOf(Guid id, string displayName) => new(
        Id: id,
        Email: $"{id:N}@example.com",
        DisplayName: displayName,
        Role: "User",
        Tier: "Free",
        CreatedAt: DateTime.UtcNow,
        IsTwoFactorEnabled: false,
        TwoFactorEnabledAt: null,
        Level: 1,
        ExperiencePoints: 0,
        EmailVerified: true,
        EmailVerifiedAt: DateTime.UtcNow,
        VerificationGracePeriodEndsAt: null,
        OnboardingCompleted: true,
        OnboardingSkipped: false);

    private static CreateSessionResult CreatedSession(Guid sessionId, Guid nightId) => new(
        sessionId, "ABC123", [], GameNightEventId: nightId, GameNightWasCreated: false,
        AgentDefinitionId: null, ToolkitId: null);

    /// <summary>A published night with the organizer and two accepted players, wired into the mocks.</summary>
    private (GameNightEvent Night, Guid Organizer, Guid Anna, Guid Luca) ArrangeNightWithRoster()
    {
        var organizer = Guid.NewGuid();
        var anna = Guid.NewGuid();
        var luca = Guid.NewGuid();
        var night = GameNightEvent.Create(
            organizer, "Serata", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        night.Publish([anna, luca]);
        night.GetRsvp(anna)!.Accept();
        night.GetRsvp(luca)!.Accept();

        _repository.Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>())).ReturnsAsync(night);
        _mediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetUserByIdQuery q, CancellationToken _) =>
                UserOf(q.UserId, q.UserId == organizer ? "Marta" : q.UserId == anna ? "Anna" : "Luca"));
        return (night, organizer, anna, luca);
    }

    [Fact]
    public async Task Handle_CreatesTheLiveGame_AdoptingTheTrackingSessionAndSeatingTheRoster()
    {
        var (night, organizer, anna, luca) = ArrangeNightWithRoster();
        var trackingSessionId = Guid.NewGuid();
        var liveSessionId = Guid.NewGuid();
        _mediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatedSession(trackingSessionId, night.Id));
        _mediator.Setup(m => m.Send(It.IsAny<CreateLiveSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(liveSessionId);

        var result = await _handler.Handle(
            new StartGameNightSessionCommand(night.Id, night.GameIds[0], "Catan", organizer),
            CancellationToken.None);

        result.LiveSessionId.Should().Be(liveSessionId);
        result.SessionId.Should().Be(trackingSessionId);
        _mediator.Verify(m => m.Send(
            It.Is<CreateLiveSessionCommand>(c =>
                c.UserId == organizer
                && c.GameName == "Catan"
                && c.GameId == night.GameIds[0]
                && c.ExistingTrackingSessionId == trackingSessionId
                // Started in the same commit: nothing in the front-end starts a live game.
                && c.StartImmediately
                && c.InitialPlayers != null
                && c.InitialPlayers.Count == 3
                // The organizer sits first (the first seat is the Host), then the accepted players.
                && c.InitialPlayers[0].UserId == organizer
                && c.InitialPlayers[0].DisplayName == "Marta"
                && c.InitialPlayers.Skip(1).Select(p => p.UserId).OrderBy(id => id)
                    .SequenceEqual(new Guid?[] { anna, luca }.OrderBy(id => id))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TheLiveGameIsCreatedAfterTheTrackingSessionAndBeforeTheAggregateIsSaved()
    {
        // Same transaction, in this order: tracking Session → live game → save of the aggregate (the link
        // itself is made in memory by AddSession between the first two, and is not a step of its own here).
        // A failure anywhere rolls the whole start back, so the aggregate must not be saved before the live
        // game. The rollback itself is proved on a real database, in GameNightLiveGameBridgeIntegrationTests.
        var (night, organizer, _, _) = ArrangeNightWithRoster();
        var calls = new List<string>();
        _mediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("tracking"))
            .ReturnsAsync(CreatedSession(Guid.NewGuid(), night.Id));
        _mediator.Setup(m => m.Send(It.IsAny<CreateLiveSessionCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("live"))
            .ReturnsAsync(Guid.NewGuid());
        _repository.Setup(r => r.UpdateAsync(night, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("save"))
            .Returns(Task.CompletedTask);

        await _handler.Handle(
            new StartGameNightSessionCommand(night.Id, night.GameIds[0], "Catan", organizer),
            CancellationToken.None);

        calls.Should().Equal("tracking", "live", "save");
    }

    [Fact]
    public async Task Handle_WhenTheLiveGameFails_NothingIsSavedAndTheErrorPropagates()
    {
        var (night, organizer, _, _) = ArrangeNightWithRoster();
        _mediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatedSession(Guid.NewGuid(), night.Id));
        _mediator.Setup(m => m.Send(It.IsAny<CreateLiveSessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("live insert failed"));

        var act = () => _handler.Handle(
            new StartGameNightSessionCommand(night.Id, night.GameIds[0], "Catan", organizer),
            CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
        _repository.Verify(r => r.UpdateAsync(It.IsAny<GameNightEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        // Live mode (a non-transactional side effect) must not open for a start that rolled back.
        _mediator.Verify(m => m.Send(It.IsAny<OpenSessionLiveModeCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AnInvalidOperationFromTheInfrastructure_IsNotAConflictAndDoesNotLeakItsMessage()
    {
        // Before #4752 every InvalidOperationException became ConflictException(ex.Message): an EF or
        // Npgsql message ("A second operation was started on this context…") reached the player as a
        // 409. It is a server error, not a conflict.
        var (night, organizer, _, _) = ArrangeNightWithRoster();
        _mediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("A second operation was started on this context"));

        var act = () => _handler.Handle(
            new StartGameNightSessionCommand(night.Id, night.GameIds[0], "Catan", organizer),
            CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Should().NotBeOfType<ConflictException>();
        thrown.Which.Message.Should().Contain("second operation");
    }

    [Fact]
    public async Task Handle_TheQuotaMessageStaysAConflictWithItsMessage()
    {
        var (night, organizer, _, _) = ArrangeNightWithRoster();
        _mediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SessionQuotaExceededException(
                "Concurrent session limit reached (3 sessions for free tier).", currentCount: 3, maxAllowed: 3));

        var act = () => _handler.Handle(
            new StartGameNightSessionCommand(night.Id, night.GameIds[0], "Catan", organizer),
            CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ConflictException>();
        thrown.Which.Message.Should().Contain("free tier");
    }

    [Fact]
    public async Task Handle_TheLiveGamesQuotaRefusal_IsAConflictWithItsMessage_AndNothingIsSavedOrOpened()
    {
        // The live game is started by StartLiveSessionCommand, which enforces the session quota by tier and
        // role — a SECOND quota next to the tracking-session one above, with its own exception type. The
        // player gets the same 409 with the message written for them, and the start rolls back whole.
        var (night, organizer, _, _) = ArrangeNightWithRoster();
        _mediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatedSession(Guid.NewGuid(), night.Id));
        _mediator.Setup(m => m.Send(It.IsAny<CreateLiveSessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new QuotaExceededException(
                "SessionQuota", "Session limit reached (3 active sessions for free tier)."));

        var act = () => _handler.Handle(
            new StartGameNightSessionCommand(night.Id, night.GameIds[0], "Catan", organizer),
            CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ConflictException>();
        thrown.Which.Message.Should().Contain("free tier");
        _repository.Verify(r => r.UpdateAsync(It.IsAny<GameNightEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<OpenSessionLiveModeCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_PlayersWithoutADisplayName_GetNeutralNames_AndNoEmailReachesTheTable(bool theNameIsTheEmail)
    {
        // A table name is shown to every player, so the fallback must not identify anybody: not the e-mail
        // address, nor a part of it. The organizer is «Organizzatore», any other player «Giocatore». The user
        // store FILLS a missing name with the address, so the case that happens for real is the second one:
        // a name that IS the e-mail (a blank check alone never fires on real data).
        var (night, organizer, anna, luca) = ArrangeNightWithRoster();
        _mediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetUserByIdQuery q, CancellationToken _) =>
                UserOf(q.UserId, displayName: theNameIsTheEmail ? $"{q.UserId:N}@example.com" : "  "));
        _mediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatedSession(Guid.NewGuid(), night.Id));
        _mediator.Setup(m => m.Send(It.IsAny<CreateLiveSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        await _handler.Handle(
            new StartGameNightSessionCommand(night.Id, night.GameIds[0], "Catan", organizer),
            CancellationToken.None);

        _mediator.Verify(m => m.Send(
            It.Is<CreateSessionCommand>(c =>
                c.Participants != null
                && c.Participants.Single(p => p.UserId == organizer).DisplayName == "Organizzatore"
                && c.Participants.Single(p => p.UserId == anna).DisplayName == "Giocatore"
                && c.Participants.Single(p => p.UserId == luca).DisplayName == "Giocatore"),
            It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(
            It.Is<CreateLiveSessionCommand>(c =>
                c.InitialPlayers != null
                && c.InitialPlayers.Count == 3
                && c.InitialPlayers[0].UserId == organizer
                && c.InitialPlayers[0].DisplayName == "Organizzatore"
                && c.InitialPlayers.Skip(1).All(p => p.DisplayName == "Giocatore")
                && c.InitialPlayers.All(p => !p.DisplayName.Contains('@'))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ANightThatCannotTakeSessions_IsAConflictWithTheAggregateMessage()
    {
        // The aggregate's own guard speaks to the player ("Cannot add sessions to a … game night").
        var organizer = Guid.NewGuid();
        var night = GameNightEvent.Create(organizer, "Serata", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        // Left in Draft: AddSession rejects it with an InvalidOperationException.
        _repository.Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>())).ReturnsAsync(night);
        _mediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserOf(organizer, "Marta"));
        _mediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatedSession(Guid.NewGuid(), night.Id));

        var act = () => _handler.Handle(
            new StartGameNightSessionCommand(night.Id, night.GameIds[0], "Catan", organizer),
            CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ConflictException>();
        thrown.Which.Message.Should().Contain("Cannot add sessions");
        _mediator.Verify(m => m.Send(It.IsAny<CreateLiveSessionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ProvidedParticipants_SeatTheLiveGameInTheSameOrder()
    {
        var (night, organizer, _, _) = ArrangeNightWithRoster();
        var other = Guid.NewGuid();
        _mediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatedSession(Guid.NewGuid(), night.Id));
        // The OWNER has the HIGHER join order: "owner first" must hold on its own, not by coincidence
        // with join order (the first seat is the Host, and the Host is the organizer).
        var participants = new List<Api.BoundedContexts.SessionTracking.Application.DTOs.ParticipantDto>
        {
            new() { Id = Guid.NewGuid(), UserId = other, DisplayName = "Second", IsOwner = false, JoinOrder = 0 },
            new() { Id = Guid.NewGuid(), UserId = organizer, DisplayName = "First", IsOwner = true, JoinOrder = 7 },
        };

        await _handler.Handle(
            new StartGameNightSessionCommand(night.Id, night.GameIds[0], "Catan", organizer, participants),
            CancellationToken.None);

        _mediator.Verify(m => m.Send(
            It.Is<CreateLiveSessionCommand>(c =>
                c.InitialPlayers != null
                && c.InitialPlayers.Select(p => p.DisplayName).SequenceEqual(new[] { "First", "Second" })),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
