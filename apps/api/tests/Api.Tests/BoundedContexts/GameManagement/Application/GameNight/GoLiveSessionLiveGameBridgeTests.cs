using Api.Tests.TestHelpers;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
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
        _unitOfWork.SetupExecuteInTransaction();
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

    private static UserDto Organizer(Guid id, string displayName) => new(
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
        // Live mode opens LAST (invariante #15), after the promotion and the live game have committed
        // together: if the live game fails, the whole transaction rolls back (link still Pending, no live
        // game) and live mode never opens — covered by the atomicity tests below.
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
    public async Task Handle_ADraftWithoutParticipants_SeatsTheOrganizer_SoTheGameCanBeStarted()
    {
        // A live game with nobody at the table cannot be started, and nothing in the front-end seats a player on
        // a game it does not own yet: with no participants, the organizer — who is going live — takes the seat.
        var sessionId = Guid.NewGuid();
        var night = NightWithDraft(sessionId, out _);
        _repository.Setup(r => r.GetByLinkedSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(night);
        _mediator.Setup(m => m.Send(It.IsAny<GetSessionDetailsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DetailsWith());
        _mediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Organizer(night.OrganizerId, "Marta"));
        _mediator.Setup(m => m.Send(It.IsAny<CreateLiveSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        await _handler.Handle(new GoLiveSessionCommand(sessionId, night.OrganizerId), CancellationToken.None);

        _mediator.Verify(m => m.Send(
            It.Is<CreateLiveSessionCommand>(c =>
                c.InitialPlayers != null
                && c.InitialPlayers.Count == 1
                && c.InitialPlayers[0].UserId == night.OrganizerId
                && c.InitialPlayers[0].DisplayName == "Marta"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ADraftWithoutParticipants_AndAnOrganizerThatDoesNotResolve_StillGoesLive()
    {
        // Seating the organizer is best-effort: if the user cannot be read, the live game is created empty
        // (and stays Created) rather than failing the go-live.
        var sessionId = Guid.NewGuid();
        var night = NightWithDraft(sessionId, out _);
        _repository.Setup(r => r.GetByLinkedSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(night);
        _mediator.Setup(m => m.Send(It.IsAny<GetSessionDetailsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DetailsWith());
        _mediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDto?)null);
        _mediator.Setup(m => m.Send(It.IsAny<CreateLiveSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        await _handler.Handle(new GoLiveSessionCommand(sessionId, night.OrganizerId), CancellationToken.None);

        _mediator.Verify(m => m.Send(
            It.Is<CreateLiveSessionCommand>(c => c.InitialPlayers != null && c.InitialPlayers.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_AnOrganizerWithoutADisplayName_IsSeatedAsOrganizzatore_NeverByEmail(bool theNameIsTheEmail)
    {
        // The table name is shown to every player of the live game: the fallback must not be the organizer's
        // e-mail address, nor a part of it. The user store FILLS a missing name with the address, so the case
        // that happens for real is the second one: a name that IS the e-mail.
        var sessionId = Guid.NewGuid();
        var night = NightWithDraft(sessionId, out _);
        _repository.Setup(r => r.GetByLinkedSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(night);
        _mediator.Setup(m => m.Send(It.IsAny<GetSessionDetailsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DetailsWith());
        _mediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Organizer(
                night.OrganizerId, theNameIsTheEmail ? $"{night.OrganizerId:N}@example.com" : "  "));
        _mediator.Setup(m => m.Send(It.IsAny<CreateLiveSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        await _handler.Handle(new GoLiveSessionCommand(sessionId, night.OrganizerId), CancellationToken.None);

        _mediator.Verify(m => m.Send(
            It.Is<CreateLiveSessionCommand>(c =>
                c.InitialPlayers != null
                && c.InitialPlayers.Count == 1
                && c.InitialPlayers[0].DisplayName == "Organizzatore"
                && !c.InitialPlayers[0].DisplayName.Contains('@')),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TheLiveGamesQuotaRefusal_IsAConflictWithItsMessage_AndLiveModeDoesNotOpen()
    {
        // The live game is started by StartLiveSessionCommand, which enforces the session quota by tier and
        // role. The message is written for the player: a 409 with it, as the night's start answers. The
        // promotion rolls back with the live game (proved on a real database).
        var sessionId = Guid.NewGuid();
        var night = NightWithDraft(sessionId, out _);
        _repository.Setup(r => r.GetByLinkedSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(night);
        _mediator.Setup(m => m.Send(It.IsAny<CreateLiveSessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new QuotaExceededException(
                "SessionQuota", "Session limit reached (3 active sessions for free tier)."));

        var act = () => _handler.Handle(new GoLiveSessionCommand(sessionId, night.OrganizerId), CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ConflictException>();
        thrown.Which.Message.Should().Contain("free tier");
        _mediator.Verify(m => m.Send(It.IsAny<OpenSessionLiveModeCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AsksForTheLiveGameToBeStarted_BecauseNothingInTheFrontEndStartsOne()
    {
        var sessionId = Guid.NewGuid();
        var night = NightWithDraft(sessionId, out _);
        _repository.Setup(r => r.GetByLinkedSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(night);
        _mediator.Setup(m => m.Send(It.IsAny<CreateLiveSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        await _handler.Handle(new GoLiveSessionCommand(sessionId, night.OrganizerId), CancellationToken.None);

        _mediator.Verify(m => m.Send(
            It.Is<CreateLiveSessionCommand>(c => c.StartImmediately), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PromotesTheLinkAndCreatesTheLiveGameInOneTransaction_AndOpensLiveModeOutsideIt()
    {
        // The promotion used to commit alone and the live game was created after it: a second go-live of the
        // same draft that read the link InProgress in that window took the self-heal branch and created a
        // SECOND live game. Both writes share one transaction now; live mode (a separate, idempotent commit)
        // stays outside it.
        var sessionId = Guid.NewGuid();
        var night = NightWithDraft(sessionId, out _);
        var insideTransaction = false;
        var calls = new List<string>();
        _unitOfWork.Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(async (Func<CancellationToken, Task> work, CancellationToken ct) =>
            {
                insideTransaction = true;
                try
                {
                    await work(ct);
                }
                finally
                {
                    insideTransaction = false;
                }
            });
        _repository.Setup(r => r.GetByLinkedSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(night);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<GameNightEvent>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add(insideTransaction ? "promote:in" : "promote:OUT"))
            .Returns(Task.CompletedTask);
        _mediator.Setup(m => m.Send(It.IsAny<CreateLiveSessionCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add(insideTransaction ? "live:in" : "live:OUT"))
            .ReturnsAsync(Guid.NewGuid());
        _mediator.Setup(m => m.Send(It.IsAny<OpenSessionLiveModeCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add(insideTransaction ? "open-live-mode:IN" : "open-live-mode:out"))
            .Returns(Task.CompletedTask);

        await _handler.Handle(new GoLiveSessionCommand(sessionId, night.OrganizerId), CancellationToken.None);

        calls.Should().Equal("promote:in", "live:in", "open-live-mode:out");
        _unitOfWork.Verify(
            u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SelfHeal_RunsTheLiveGameInATransactionToo_WithoutWritingTheAggregate()
    {
        var sessionId = Guid.NewGuid();
        var night = NightWithDraft(sessionId, out _);
        night.StartSession(sessionId); // link already InProgress
        _repository.Setup(r => r.GetByLinkedSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(night);
        _mediator.Setup(m => m.Send(It.IsAny<CreateLiveSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        await _handler.Handle(new GoLiveSessionCommand(sessionId, night.OrganizerId), CancellationToken.None);

        _unitOfWork.Verify(
            u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<GameNightEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTheLiveGameFails_TheErrorPropagatesAndLiveModeDoesNotOpen()
    {
        // The rollback of the promotion with it is proved on a real database (integration tests); here, that the
        // failure is not swallowed and live mode — which would split-brain the link — does not open.
        var sessionId = Guid.NewGuid();
        var night = NightWithDraft(sessionId, out _);
        _repository.Setup(r => r.GetByLinkedSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(night);
        _mediator.Setup(m => m.Send(It.IsAny<CreateLiveSessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("live insert failed"));

        var act = () => _handler.Handle(new GoLiveSessionCommand(sessionId, night.OrganizerId), CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
        _mediator.Verify(m => m.Send(It.IsAny<OpenSessionLiveModeCommand>(), It.IsAny<CancellationToken>()), Times.Never);
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
