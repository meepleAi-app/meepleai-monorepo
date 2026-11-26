using Api.Tests.TestHelpers;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Exceptions;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Exceptions;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// #2632 (SI-1b): the attach-and-start "writer" of the Session ↔ GamebookCampaign link.
/// Guards (shared-only, organizer), cross-BC dispatch, and the AddSession → StartCurrentSession sequence.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class AttachGamebookCampaignToGameNightCommandHandlerTests
{
    private readonly Mock<IGameNightEventRepository> _repo = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IAutoSaveSchedulerService> _autoSave = new();
    private readonly AttachGamebookCampaignToGameNightCommandHandler _handler;

    public AttachGamebookCampaignToGameNightCommandHandlerTests()
    {
        // #3636: l'handler consegna il lavoro alla UoW invece di pilotare Begin/Commit.
        // Senza questo setup il mock non esegue il delegate e nulla accade.
        _uow.SetupExecuteInTransaction<AttachGamebookCampaignToGameNightResult>();
        _handler = new AttachGamebookCampaignToGameNightCommandHandler(
            _repo.Object, _mediator.Object, _uow.Object, _autoSave.Object);
    }

    private static GameNightEvent CreatePublishedEvent(Guid organizerId)
    {
        var evt = GameNightEvent.Create(
            organizerId, "Serata da Marco", DateTimeOffset.UtcNow.AddHours(1),
            gameIds: [Guid.NewGuid()]);
        evt.Publish([]);
        return evt;
    }

    private static GamebookCampaignDto CampaignDto(Guid campaignId, Guid gameId, Guid ownerId, GameRefKind kind)
        => new(
            Id: campaignId,
            GameRefId: gameId,
            GameRefKind: (int)kind,
            OwnerUserId: ownerId,
            Title: "La Runa di Eldoria",
            CurrentParagraph: 0,
            History: Array.Empty<int>(),
            LastReadAt: DateTimeOffset.UtcNow,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);

    private void SetupCampaign(Guid campaignId, GamebookCampaignDto dto)
        => _mediator
            .Setup(m => m.Send(It.Is<GetGamebookCampaignQuery>(q => q.CampaignId == campaignId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

    private void SetupCreateSession(Guid sessionId, string code = "ABC123")
        => _mediator
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateSessionResult(sessionId, code, [], Guid.Empty, false, null, null));

    private void SetupUser(Guid userId, string displayName)
        => _mediator
            .Setup(m => m.Send(It.Is<GetUserByIdQuery>(q => q.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDto(
                Id: userId,
                Email: "player@example.com",
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
                OnboardingSkipped: false));

    [Fact]
    public async Task Handle_SharedCampaign_CreatesLinkedSessionAndStarts()
    {
        var organizer = Guid.NewGuid();
        var gameNight = CreatePublishedEvent(organizer);
        var campaignId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        SetupCampaign(campaignId, CampaignDto(campaignId, Guid.NewGuid(), organizer, GameRefKind.Shared));
        SetupCreateSession(sessionId);
        SetupUser(organizer, "Marco Rossi");
        _repo.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>())).ReturnsAsync(gameNight);

        var result = await _handler.Handle(
            new AttachGamebookCampaignToGameNightCommand(gameNight.Id, campaignId, organizer),
            TestContext.Current.CancellationToken);

        result.SessionId.Should().Be(sessionId);
        result.SessionCode.Should().Be("ABC123");
        result.PlayOrder.Should().Be(1);
        gameNight.Sessions.Should().ContainSingle();
        gameNight.Sessions[0].Status.Should().Be(GameNightSessionStatus.InProgress);
        _repo.Verify(r => r.UpdateAsync(gameNight, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(
            u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<AttachGamebookCampaignToGameNightResult>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DispatchesCreateSessionCarryingTheCampaignLink()
    {
        var organizer = Guid.NewGuid();
        var gameNight = CreatePublishedEvent(organizer);
        var campaignId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        SetupCampaign(campaignId, CampaignDto(campaignId, gameId, organizer, GameRefKind.Shared));
        SetupCreateSession(Guid.NewGuid());
        SetupUser(organizer, "Marco Rossi");
        _repo.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>())).ReturnsAsync(gameNight);

        await _handler.Handle(
            new AttachGamebookCampaignToGameNightCommand(gameNight.Id, campaignId, organizer),
            TestContext.Current.CancellationToken);

        _mediator.Verify(m => m.Send(
            It.Is<CreateSessionCommand>(c =>
                c.GamebookCampaignId == campaignId &&
                c.GameId == gameId &&
                c.UserId == organizer &&
                c.Participants.Count == 1 &&
                c.Participants[0].IsOwner &&
                c.Participants[0].DisplayName == "Marco Rossi"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// #4780, review of #5161 — the organizer's name seats them in the campaign's tracking session, and from there
    /// the roster and the winner of the night: never an address, theirs (<c>player@example.com</c>) or another.
    /// </summary>
    [Theory]
    [Trait("Issue", "4780")]
    [InlineData("player@example.com", "Organizzatore")]
    [InlineData("Marco (player@example.com)", "Organizzatore")]
    [InlineData("marco@vecchio.it", "Organizzatore")]
    [InlineData("Marco Rossi", "Marco Rossi")]
    public async Task Handle_TheOrganizerIsNeverSeatedUnderAnAddress(string profileName, string seated)
    {
        var organizer = Guid.NewGuid();
        var gameNight = CreatePublishedEvent(organizer);
        var campaignId = Guid.NewGuid();
        SetupCampaign(campaignId, CampaignDto(campaignId, Guid.NewGuid(), organizer, GameRefKind.Shared));
        SetupCreateSession(Guid.NewGuid());
        SetupUser(organizer, profileName);
        _repo.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>())).ReturnsAsync(gameNight);

        await _handler.Handle(
            new AttachGamebookCampaignToGameNightCommand(gameNight.Id, campaignId, organizer),
            TestContext.Current.CancellationToken);

        _mediator.Verify(m => m.Send(
            It.Is<CreateSessionCommand>(c => c.Participants.Single(p => p.IsOwner).DisplayName == seated),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_HappyPath_WrapsWorkInCommittedTransaction()
    {
        var organizer = Guid.NewGuid();
        var gameNight = CreatePublishedEvent(organizer);
        var campaignId = Guid.NewGuid();
        SetupCampaign(campaignId, CampaignDto(campaignId, Guid.NewGuid(), organizer, GameRefKind.Shared));
        SetupCreateSession(Guid.NewGuid());
        SetupUser(organizer, "Marco Rossi");
        _repo.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>())).ReturnsAsync(gameNight);

        await _handler.Handle(
            new AttachGamebookCampaignToGameNightCommand(gameNight.Id, campaignId, organizer),
            TestContext.Current.CancellationToken);

        // #3636: Begin/Commit non sono più dell'handler — la sequenza è interna alla UoW e
        // coperta dai suoi test. Qui conta che il lavoro sia passato da UNA transazione e che
        // l'handler non ne apra di proprie (fallirebbero sotto la retry strategy).
        _uow.Verify(
            u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<AttachGamebookCampaignToGameNightResult>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ConcurrentXminLoser_RollsBackTransaction_Throws409()
    {
        // The xmin loser's aggregate SaveChanges throws; the whole tx (incl. the cross-BC Session
        // INSERT) must roll back so no orphan Session survives, then map to the blocked-modal 409.
        var organizer = Guid.NewGuid();
        var gameNight = CreatePublishedEvent(organizer);
        var campaignId = Guid.NewGuid();
        SetupCampaign(campaignId, CampaignDto(campaignId, Guid.NewGuid(), organizer, GameRefKind.Shared));
        SetupCreateSession(Guid.NewGuid());
        SetupUser(organizer, "Marco Rossi");
        _repo.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>())).ReturnsAsync(gameNight);
        // #3636: il conflitto emerge dal SaveChanges interno alla UoW → si simula facendo fallire
        // ExecuteInTransactionAsync; mockare SaveChangesAsync non avrebbe più effetto.
        _uow.Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<AttachGamebookCampaignToGameNightResult>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var act = () => _handler.Handle(
            new AttachGamebookCampaignToGameNightCommand(gameNight.Id, campaignId, organizer),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<MaxLiveSessionsExceededException>();
        // Il rollback è responsabilità della UoW: qui conta la mappatura a 409.
        _mediator.Verify(m => m.Send(It.IsAny<OpenSessionLiveModeCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SecondSittingOnInProgressNight_AttachesAndStarts()
    {
        // SI-4 (#2635): resuming a gamebook campaign for a 2nd sitting on an InProgress night — the
        // path the "▶ Riprendi" wire (step 3) drives. Before the AddSession relax this 409'd
        // (Published-only guard). EnsureCanStartSession passes (no live session) and the night stays
        // InProgress after attaching + starting the new sitting.
        var organizer = Guid.NewGuid();
        var gameNight = CreatePublishedEvent(organizer);
        var firstSessionId = Guid.NewGuid();
        gameNight.AddSession(firstSessionId, gameNight.GameIds[0], "Prima partita");
        gameNight.StartCurrentSession();
        gameNight.HandleFirstSessionStarted(firstSessionId); // Published → InProgress
        gameNight.CompleteCurrentSession(winnerId: null);     // no live session
        gameNight.Status.Should().Be(GameNightStatus.InProgress);

        var campaignId = Guid.NewGuid();
        var secondSessionId = Guid.NewGuid();
        SetupCampaign(campaignId, CampaignDto(campaignId, Guid.NewGuid(), organizer, GameRefKind.Shared));
        SetupCreateSession(secondSessionId, "SND222");
        SetupUser(organizer, "Marco Rossi");
        _repo.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>())).ReturnsAsync(gameNight);

        var result = await _handler.Handle(
            new AttachGamebookCampaignToGameNightCommand(gameNight.Id, campaignId, organizer),
            TestContext.Current.CancellationToken);

        result.SessionId.Should().Be(secondSessionId);
        result.PlayOrder.Should().Be(2);
        gameNight.Sessions.Should().HaveCount(2);
        gameNight.Sessions[1].Status.Should().Be(GameNightSessionStatus.InProgress);
        gameNight.Status.Should().Be(GameNightStatus.InProgress);
    }

    [Fact]
    public async Task Handle_NightWithAPendingDraft_StartsTheNewSitting_AndLeavesTheDraftPending()
    {
        // #4780: the handler added the sitting and then called StartCurrentSession, which promotes the FIRST
        // pending link by play order — on a night with a planned draft, the draft, not the sitting just added.
        var organizer = Guid.NewGuid();
        var gameNight = CreatePublishedEvent(organizer);
        var draftSessionId = Guid.NewGuid();
        gameNight.AddSession(draftSessionId, gameNight.GameIds[0], "Bozza pianificata");

        var campaignId = Guid.NewGuid();
        var sittingSessionId = Guid.NewGuid();
        SetupCampaign(campaignId, CampaignDto(campaignId, Guid.NewGuid(), organizer, GameRefKind.Shared));
        SetupCreateSession(sittingSessionId, "SIT333");
        SetupUser(organizer, "Marco Rossi");
        _repo.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>())).ReturnsAsync(gameNight);

        await _handler.Handle(
            new AttachGamebookCampaignToGameNightCommand(gameNight.Id, campaignId, organizer),
            TestContext.Current.CancellationToken);

        gameNight.Sessions.Single(s => s.SessionId == sittingSessionId).Status
            .Should().Be(GameNightSessionStatus.InProgress, "the sitting just attached is the one being played");
        gameNight.Sessions.Single(s => s.SessionId == draftSessionId).Status
            .Should().Be(GameNightSessionStatus.Pending, "the planned draft was not started");
    }

    [Fact]
    public async Task Handle_SecondSittingWhileFirstStillLive_Throws409MaxLive_NoSessionCreated()
    {
        // AC5: max-1-live holds on the gamebook resume path (the one the play-page CTA drives).
        // Attaching a 2nd campaign while the first sitting is still live is rejected by
        // EnsureCanStartSession (guard-before-create) — no cross-BC Session is minted.
        var organizer = Guid.NewGuid();
        var gameNight = CreatePublishedEvent(organizer);
        var firstSessionId = Guid.NewGuid();
        gameNight.AddSession(firstSessionId, gameNight.GameIds[0], "Prima partita");
        gameNight.StartCurrentSession();                     // first sitting InProgress
        gameNight.HandleFirstSessionStarted(firstSessionId); // night InProgress

        var campaignId = Guid.NewGuid();
        SetupCampaign(campaignId, CampaignDto(campaignId, Guid.NewGuid(), organizer, GameRefKind.Shared));
        _repo.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>())).ReturnsAsync(gameNight);

        var act = () => _handler.Handle(
            new AttachGamebookCampaignToGameNightCommand(gameNight.Id, campaignId, organizer),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<MaxLiveSessionsExceededException>();
        _mediator.Verify(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PrivateGameCampaign_ThrowsConflict_AndDoesNotCreateSession()
    {
        var organizer = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        SetupCampaign(campaignId, CampaignDto(campaignId, Guid.NewGuid(), organizer, GameRefKind.Private));

        var act = () => _handler.Handle(
            new AttachGamebookCampaignToGameNightCommand(Guid.NewGuid(), campaignId, organizer),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>();
        _mediator.Verify(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NonOrganizer_ThrowsForbidden_AndDoesNotCreateSession()
    {
        var organizer = Guid.NewGuid();
        var caller = Guid.NewGuid();
        var gameNight = CreatePublishedEvent(organizer);
        var campaignId = Guid.NewGuid();
        // The campaign is owned by the caller (query passes) but the caller is not the GameNight organizer.
        SetupCampaign(campaignId, CampaignDto(campaignId, Guid.NewGuid(), caller, GameRefKind.Shared));
        _repo.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>())).ReturnsAsync(gameNight);

        var act = () => _handler.Handle(
            new AttachGamebookCampaignToGameNightCommand(gameNight.Id, campaignId, caller),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
        _mediator.Verify(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_GameNightNotFound_ThrowsNotFound()
    {
        var organizer = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var gnId = Guid.NewGuid();
        SetupCampaign(campaignId, CampaignDto(campaignId, Guid.NewGuid(), organizer, GameRefKind.Shared));
        _repo.Setup(r => r.GetByIdAsync(gnId, It.IsAny<CancellationToken>())).ReturnsAsync((GameNightEvent?)null);

        var act = () => _handler.Handle(
            new AttachGamebookCampaignToGameNightCommand(gnId, campaignId, organizer),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CampaignQueryThrows_Propagates_AndDoesNotTouchGameNight()
    {
        var organizer = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        _mediator
            .Setup(m => m.Send(It.IsAny<GetGamebookCampaignQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Campaign", campaignId.ToString()));

        var act = () => _handler.Handle(
            new AttachGamebookCampaignToGameNightCommand(Guid.NewGuid(), campaignId, organizer),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
        _repo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// #5158 — the catch of InvalidOperationException now wraps only AddSession and StartSession, as on the start
    /// (#4752): with the night read inside the work, a wide catch would turn an EF failure into a 409 that carries
    /// its message. The quota of the tracking session, an InvalidOperationException too, keeps its 409.
    /// </summary>
    [Fact]
    [Trait("Issue", "5158")]
    public async Task Handle_AnInvalidOperationOutsideTheAggregate_IsNotTurnedIntoAConflict()
    {
        var organizer = Guid.NewGuid();
        var gameNight = CreatePublishedEvent(organizer);
        var campaignId = Guid.NewGuid();
        SetupCampaign(campaignId, CampaignDto(campaignId, Guid.NewGuid(), organizer, GameRefKind.Shared));
        SetupCreateSession(Guid.NewGuid());
        SetupUser(organizer, "Marco");
        _repo.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>())).ReturnsAsync(gameNight);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<GameNightEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The instance of entity type cannot be tracked"));

        var act = () => _handler.Handle(
            new AttachGamebookCampaignToGameNightCommand(gameNight.Id, campaignId, organizer),
            TestContext.Current.CancellationToken);

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Should().NotBeOfType<ConflictException>();
    }

    /// <summary>
    /// #5167 — two attaches (or an attach and a start) that both passed the guard in memory meet at one of the two
    /// unique indexes of the night's links. The start answered 409 since #4780; the attach let the DbUpdateException
    /// out, a 500. Another index stays a DbUpdateException: only these two are a race between two writers.
    /// </summary>
    [Theory]
    [Trait("Issue", "5167")]
    [InlineData("ix_game_night_sessions_unique_active", typeof(MaxLiveSessionsExceededException))]
    [InlineData("IX_game_night_sessions_event_play_order", typeof(ConflictException))]
    [InlineData("some_other_index", typeof(DbUpdateException))]
    public async Task Handle_TwoWritersMeetingAtALinkIndex_IsAConflict(string index, Type expected)
    {
        Api.Infrastructure.EntityConfigurations.GameManagement.GameNightSessionEntityConfiguration.UniqueActiveIndexName
            .Should().Be("ix_game_night_sessions_unique_active", "the case names the index the code matches");
        Api.Infrastructure.EntityConfigurations.GameManagement.GameNightSessionEntityConfiguration.PlayOrderIndexName
            .Should().Be("IX_game_night_sessions_event_play_order", "the case names the index the code matches");

        var organizer = Guid.NewGuid();
        var gameNight = CreatePublishedEvent(organizer);
        var campaignId = Guid.NewGuid();
        SetupCampaign(campaignId, CampaignDto(campaignId, Guid.NewGuid(), organizer, GameRefKind.Shared));
        SetupCreateSession(Guid.NewGuid());
        SetupUser(organizer, "Marco");
        _repo.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>())).ReturnsAsync(gameNight);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<GameNightEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("unique violation", new Npgsql.PostgresException(
                "duplicate key value violates unique constraint", "ERROR", "ERROR", "23505", constraintName: index)));

        var act = () => _handler.Handle(
            new AttachGamebookCampaignToGameNightCommand(gameNight.Id, campaignId, organizer),
            TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<Exception>()).Which.Should().BeOfType(expected);
    }

    [Fact]
    [Trait("Issue", "5158")]
    public async Task Handle_TheTrackingSessionQuota_IsStillAConflictWithItsMessage()
    {
        var organizer = Guid.NewGuid();
        var gameNight = CreatePublishedEvent(organizer);
        var campaignId = Guid.NewGuid();
        SetupCampaign(campaignId, CampaignDto(campaignId, Guid.NewGuid(), organizer, GameRefKind.Shared));
        SetupUser(organizer, "Marco");
        _repo.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>())).ReturnsAsync(gameNight);
        _mediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SessionQuotaExceededException("Hai raggiunto il limite di 3 sessioni.", 3, 3));

        var act = () => _handler.Handle(
            new AttachGamebookCampaignToGameNightCommand(gameNight.Id, campaignId, organizer),
            TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Message.Should().Be("Hai raggiunto il limite di 3 sessioni.");
    }

    /// <summary>
    /// #5158 — the strategy runs the work again after a transient failure. The night was read before the work, so
    /// the retry found the aggregate the failed attempt had already given an in-progress sitting, and StartSession
    /// refused the new one with a 409. Every attempt now reads the night as it is stored.
    /// </summary>
    [Fact]
    [Trait("Issue", "5158")]
    public async Task Handle_RetriedAfterATransientFailure_AttachesToTheNightAsStored()
    {
        var organizer = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        SetupCampaign(campaignId, CampaignDto(campaignId, Guid.NewGuid(), organizer, GameRefKind.Shared));
        SetupCreateSession(Guid.NewGuid());
        SetupUser(organizer, "Marco");

        // A repository reads the stored night again on every call: a new aggregate, with nothing in progress.
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreatePublishedEvent(organizer));
        var saved = new List<GameNightEvent>();
        _repo.Setup(r => r.UpdateAsync(It.IsAny<GameNightEvent>(), It.IsAny<CancellationToken>()))
            .Callback<GameNightEvent, CancellationToken>((night, _) => saved.Add(night))
            .Returns(Task.CompletedTask);
        var handler = new AttachGamebookCampaignToGameNightCommandHandler(
            _repo.Object, _mediator.Object,
            new Mock<IUnitOfWork>().SetupExecuteInTransactionRetriedOnce<AttachGamebookCampaignToGameNightResult>().Object,
            _autoSave.Object);

        await handler.Handle(
            new AttachGamebookCampaignToGameNightCommand(Guid.NewGuid(), campaignId, organizer),
            TestContext.Current.CancellationToken);

        saved.Should().HaveCount(2, "each attempt saves its night");
        saved[1].Should().NotBeSameAs(saved[0], "the retry works on the night read again");
        saved[1].Sessions.Should().ContainSingle().Which.Status.Should().Be(GameNightSessionStatus.InProgress);
    }
}
