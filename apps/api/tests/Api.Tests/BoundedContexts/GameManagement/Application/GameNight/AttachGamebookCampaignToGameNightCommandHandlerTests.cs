using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Exceptions;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
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
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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

        _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
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
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var act = () => _handler.Handle(
            new AttachGamebookCampaignToGameNightCommand(gameNight.Id, campaignId, organizer),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<MaxLiveSessionsExceededException>();
        _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
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
}
