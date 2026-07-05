using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.BoundedContexts.GameManagement.Domain.Exceptions;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// Unit tests for StartGameNightSessionCommandHandler.
/// Validates cross-BC session creation via MediatR and game night aggregate linking.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class StartGameNightSessionCommandHandlerTests
{
    private readonly Mock<IGameNightEventRepository> _mockRepository;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IAutoSaveSchedulerService> _mockAutoSaveScheduler;
    private readonly StartGameNightSessionCommandHandler _handler;

    public StartGameNightSessionCommandHandlerTests()
    {
        _mockRepository = new Mock<IGameNightEventRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockAutoSaveScheduler = new Mock<IAutoSaveSchedulerService>();
        _handler = new StartGameNightSessionCommandHandler(
            _mockRepository.Object,
            _mockMediator.Object,
            _mockUnitOfWork.Object,
            _mockAutoSaveScheduler.Object);
    }

    private static GameNightEvent CreatePublishedEvent(Guid? organizerId = null)
    {
        var orgId = organizerId ?? Guid.NewGuid();
        var evt = GameNightEvent.Create(
            orgId, "Friday Night", DateTimeOffset.UtcNow.AddHours(1),
            gameIds: [Guid.NewGuid(), Guid.NewGuid()]);
        evt.Publish([]);
        return evt;
    }

    private static UserDto CreateOrganizerDto(Guid organizerId, string displayName = "Test Organizer")
    {
        return new UserDto(
            Id: organizerId,
            Email: "organizer@example.com",
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
    }

    [Fact]
    public async Task Handle_CreatesSessionAndLinksToGameNight()
    {
        // Arrange
        var gameNight = CreatePublishedEvent();
        var gameId = gameNight.GameIds[0];
        var newSessionId = Guid.NewGuid();

        _mockRepository.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        _mockMediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrganizerDto(gameNight.OrganizerId));

        _mockMediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateSessionResult(
                newSessionId,
                "ABC123",
                [],
                GameNightEventId: gameNight.Id,
                GameNightWasCreated: false,
                AgentDefinitionId: null,
                ToolkitId: null));

        var command = new StartGameNightSessionCommand(
            gameNight.Id, gameId, "Catan", gameNight.OrganizerId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(newSessionId, result.SessionId);
        Assert.Equal("ABC123", result.SessionCode);
        Assert.Equal(1, result.PlayOrder);
        Assert.Single(gameNight.Sessions);

        _mockRepository.Verify(r => r.UpdateAsync(gameNight, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NonOrganizer_ThrowsForbidden()
    {
        // Arrange
        var gameNight = CreatePublishedEvent();
        var differentUserId = Guid.NewGuid();

        _mockRepository.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        var command = new StartGameNightSessionCommand(
            gameNight.Id, Guid.NewGuid(), "Catan", differentUserId);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.Handle(command, CancellationToken.None));

        _mockMediator.Verify(
            m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_GameNightNotFound_ThrowsNotFound()
    {
        // Arrange
        var gameNightId = Guid.NewGuid();

        _mockRepository.Setup(r => r.GetByIdAsync(gameNightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightEvent?)null);

        var command = new StartGameNightSessionCommand(
            gameNightId, Guid.NewGuid(), "Catan", Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DispatchesCreateSessionCommandWithAutoSeededOrganizer()
    {
        // Arrange
        var gameNight = CreatePublishedEvent();
        var gameId = gameNight.GameIds[0];

        _mockRepository.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        _mockMediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrganizerDto(gameNight.OrganizerId, displayName: "Alice Organizer"));

        _mockMediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateSessionResult(
                Guid.NewGuid(),
                "XYZ789",
                [],
                GameNightEventId: gameNight.Id,
                GameNightWasCreated: false,
                AgentDefinitionId: null,
                ToolkitId: null));

        var command = new StartGameNightSessionCommand(
            gameNight.Id, gameId, "Catan", gameNight.OrganizerId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — auto-seeded organizer becomes sole owner
        _mockMediator.Verify(m => m.Send(
            It.Is<CreateSessionCommand>(c =>
                c.UserId == gameNight.OrganizerId &&
                c.GameId == gameId &&
                c.SessionType == "GameSpecific" &&
                c.Participants.Count == 1 &&
                c.Participants[0].IsOwner &&
                c.Participants[0].UserId == gameNight.OrganizerId &&
                c.Participants[0].DisplayName == "Alice Organizer"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoParticipants_SeedsOrganizerOwnerPlusAcceptedRsvpPlayers()
    {
        // #2634 C4: when the start command carries no explicit roster, seed from the night —
        // the organizer as owner + every ACCEPTED-RSVP player (Pending/Declined excluded).
        var organizerId = Guid.NewGuid();
        var player2 = Guid.NewGuid();
        var player3 = Guid.NewGuid();
        var pendingPlayer = Guid.NewGuid();
        var gameNight = GameNightEvent.Create(
            organizerId, "Serata", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        gameNight.Publish([player2, player3, pendingPlayer]); // 3 invited → Pending RSVPs
        gameNight.GetRsvp(player2)!.Accept();
        gameNight.GetRsvp(player3)!.Accept();
        // pendingPlayer stays Pending → excluded from the seeded roster

        _mockRepository.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetUserByIdQuery q, CancellationToken _) =>
                CreateOrganizerDto(q.UserId, $"User-{q.UserId.ToString()[..4]}"));
        _mockMediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateSessionResult(
                Guid.NewGuid(), "ABC123", [], GameNightEventId: gameNight.Id,
                GameNightWasCreated: false, AgentDefinitionId: null, ToolkitId: null));

        var command = new StartGameNightSessionCommand(
            gameNight.Id, gameNight.GameIds[0], "Catan", organizerId);

        await _handler.Handle(command, CancellationToken.None);

        _mockMediator.Verify(m => m.Send(
            It.Is<CreateSessionCommand>(c =>
                c.Participants.Count == 3 &&
                c.Participants.Count(p => p.IsOwner) == 1 &&
                c.Participants.Any(p => p.UserId == organizerId && p.IsOwner) &&
                c.Participants.Any(p => p.UserId == player2 && !p.IsOwner) &&
                c.Participants.Any(p => p.UserId == player3 && !p.IsOwner) &&
                c.Participants.All(p => p.UserId != pendingPlayer)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithProvidedParticipants_UsesThemDirectly()
    {
        // Arrange
        var gameNight = CreatePublishedEvent();
        var customParticipants = new List<ParticipantDto>
        {
            new() {
                Id = Guid.NewGuid(),
                UserId = gameNight.OrganizerId,
                DisplayName = "Host Bob",
                IsOwner = true,
                JoinOrder = 0
            },
            new() {
                Id = Guid.NewGuid(),
                DisplayName = "Guest Charlie",
                IsOwner = false,
                JoinOrder = 1
            }
        };

        _mockRepository.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        _mockMediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateSessionResult(
                Guid.NewGuid(),
                "ABC123",
                [],
                GameNightEventId: gameNight.Id,
                GameNightWasCreated: false,
                AgentDefinitionId: null,
                ToolkitId: null));

        var command = new StartGameNightSessionCommand(
            gameNight.Id, gameNight.GameIds[0], "Catan",
            gameNight.OrganizerId, customParticipants);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — provided participants are passed through, no user lookup performed
        _mockMediator.Verify(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockMediator.Verify(m => m.Send(
            It.Is<CreateSessionCommand>(c =>
                c.Participants.Count == 2 &&
                c.Participants.Any(p => p.DisplayName == "Host Bob" && p.IsOwner) &&
                c.Participants.Any(p => p.DisplayName == "Guest Charlie" && !p.IsOwner)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithProvidedParticipantsLackingOwner_ThrowsConflict()
    {
        // Arrange
        var gameNight = CreatePublishedEvent();
        var noOwnerParticipants = new List<ParticipantDto>
        {
            new() { Id = Guid.NewGuid(), DisplayName = "NonOwner", IsOwner = false, JoinOrder = 0 }
        };

        _mockRepository.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        var command = new StartGameNightSessionCommand(
            gameNight.Id, gameNight.GameIds[0], "Catan",
            gameNight.OrganizerId, noOwnerParticipants);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));

        _mockMediator.Verify(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_OrganizerNotFoundInUserQuery_ThrowsNotFound()
    {
        // Arrange
        var gameNight = CreatePublishedEvent();

        _mockRepository.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        _mockMediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDto?)null);

        var command = new StartGameNightSessionCommand(
            gameNight.Id, gameNight.GameIds[0], "Catan", gameNight.OrganizerId);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));

        _mockMediator.Verify(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_HappyPath_WrapsWorkInCommittedTransaction()
    {
        // Arrange — the cross-BC Session INSERT + the aggregate link must be ONE atomic tx so a
        // later failure cannot leave the Session orphaned (WS1 DEC-5 hardening).
        var gameNight = CreatePublishedEvent();

        _mockRepository.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrganizerDto(gameNight.OrganizerId));
        _mockMediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateSessionResult(
                Guid.NewGuid(), "ABC123", [], GameNightEventId: gameNight.Id,
                GameNightWasCreated: false, AgentDefinitionId: null, ToolkitId: null));

        var command = new StartGameNightSessionCommand(
            gameNight.Id, gameNight.GameIds[0], "Catan", gameNight.OrganizerId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — begun + committed exactly once, never rolled back.
        _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ConcurrentXminLoser_RollsBackTransaction_Throws409()
    {
        // Arrange — two concurrent starts both pass the in-memory guard; the xmin loser's aggregate
        // SaveChanges throws DbUpdateConcurrencyException. The whole tx (incl. the cross-BC Session
        // INSERT) must roll back so no orphan Session survives, then map to the blocked-modal 409.
        var gameNight = CreatePublishedEvent();

        _mockRepository.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrganizerDto(gameNight.OrganizerId));
        _mockMediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateSessionResult(
                Guid.NewGuid(), "ABC123", [], GameNightEventId: gameNight.Id,
                GameNightWasCreated: false, AgentDefinitionId: null, ToolkitId: null));
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var command = new StartGameNightSessionCommand(
            gameNight.Id, gameNight.GameIds[0], "Catan", gameNight.OrganizerId);

        // Act & Assert
        await Assert.ThrowsAsync<MaxLiveSessionsExceededException>(
            () => _handler.Handle(command, CancellationToken.None));

        _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        // The blocked start must NOT open live mode on a rolled-back session.
        _mockMediator.Verify(m => m.Send(It.IsAny<OpenSessionLiveModeCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SessionStartedAfterAdding()
    {
        // Arrange
        var gameNight = CreatePublishedEvent();
        var sessionId = Guid.NewGuid();

        _mockRepository.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        _mockMediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrganizerDto(gameNight.OrganizerId));

        _mockMediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateSessionResult(
                sessionId,
                "DEF456",
                [],
                GameNightEventId: gameNight.Id,
                GameNightWasCreated: false,
                AgentDefinitionId: null,
                ToolkitId: null));

        var command = new StartGameNightSessionCommand(
            gameNight.Id, gameNight.GameIds[0], "Catan", gameNight.OrganizerId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — session should be in InProgress status after Start
        var gns = gameNight.Sessions[0];
        Assert.Equal(GameNightSessionStatus.InProgress, gns.Status);
    }
}
