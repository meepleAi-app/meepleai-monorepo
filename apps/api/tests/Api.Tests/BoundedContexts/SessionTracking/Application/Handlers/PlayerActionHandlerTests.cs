using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

/// <summary>
/// Unit tests for player action command handlers.
/// Issue #4765 - Player Action Endpoints + Host Validation
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class MarkPlayerReadyCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _mockSessionRepo;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ISessionSyncService> _mockSyncService;
    private readonly MarkPlayerReadyCommandHandler _handler;

    public MarkPlayerReadyCommandHandlerTests()
    {
        _mockSessionRepo = new Mock<ISessionRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockSyncService = new Mock<ISessionSyncService>();
        _handler = new MarkPlayerReadyCommandHandler(
            _mockSessionRepo.Object,
            _mockUnitOfWork.Object,
            _mockSyncService.Object);
    }

    private static Session CreateActiveSessionWithParticipant(
        Guid sessionId, Guid participantId, bool isOwner = false)
    {
        var session = Session.Create(
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            sessionType: SessionType.GameSpecific);

        typeof(Session).GetProperty("Id")!.SetValue(session, sessionId);

        var participant = new Participant
        {
            Id = participantId,
            SessionId = sessionId,
            DisplayName = "Player 1",
            IsOwner = isOwner,
            Role = isOwner ? ParticipantRole.Host : ParticipantRole.Player,
            IsReady = false
        };

        var participantsField = typeof(Session).GetField("_participants",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = (List<Participant>)participantsField!.GetValue(session)!;
        list.Add(participant);

        return session;
    }

    [Fact]
    public async Task Handle_ValidCommand_MarksPlayerReady()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var session = CreateActiveSessionWithParticipant(sessionId, participantId);

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new MarkPlayerReadyCommand(sessionId, participantId, requesterId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsReady.Should().BeTrue();
        _mockSessionRepo.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockSyncService.Verify(s => s.PublishEventAsync(
            sessionId, It.IsAny<MediatR.INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var command = new MarkPlayerReadyCommand(sessionId, Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_FinalizedSession_ThrowsConflictException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var session = CreateActiveSessionWithParticipant(sessionId, participantId);

        // Finalize the session
        session.Finalize();

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new MarkPlayerReadyCommand(sessionId, participantId, Guid.NewGuid());

        // Act & Assert
        var act2 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act2.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_ParticipantNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateActiveSessionWithParticipant(sessionId, Guid.NewGuid());

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Use a non-existent participant ID
        var command = new MarkPlayerReadyCommand(sessionId, Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        var act3 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act3.Should().ThrowAsync<NotFoundException>();
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class KickParticipantCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _mockSessionRepo;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ISessionSyncService> _mockSyncService;
    private readonly KickParticipantCommandHandler _handler;

    public KickParticipantCommandHandlerTests()
    {
        _mockSessionRepo = new Mock<ISessionRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockSyncService = new Mock<ISessionSyncService>();
        _handler = new KickParticipantCommandHandler(
            _mockSessionRepo.Object,
            _mockUnitOfWork.Object,
            _mockSyncService.Object);
    }

    private static Session CreateSessionWithPlayers(
        Guid sessionId, Guid ownerId, Guid playerId)
    {
        var session = Session.Create(
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            sessionType: SessionType.GameSpecific);

        typeof(Session).GetProperty("Id")!.SetValue(session, sessionId);

        var participantsField = typeof(Session).GetField("_participants",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = (List<Participant>)participantsField!.GetValue(session)!;

        list.Add(new Participant
        {
            Id = ownerId,
            SessionId = sessionId,
            DisplayName = "Host",
            IsOwner = true,
            Role = ParticipantRole.Host
        });

        list.Add(new Participant
        {
            Id = playerId,
            SessionId = sessionId,
            DisplayName = "Player",
            IsOwner = false,
            Role = ParticipantRole.Player
        });

        return session;
    }

    [Fact]
    public async Task Handle_ValidKick_RemovesParticipantAndPublishesEvent()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var session = CreateSessionWithPlayers(sessionId, ownerId, playerId);

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new KickParticipantCommand(sessionId, playerId, ownerId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.ParticipantId.Should().Be(playerId);
        result.DisplayName.Should().Be("Player");
        _mockSessionRepo.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockSyncService.Verify(s => s.PublishEventAsync(
            sessionId, It.IsAny<MediatR.INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var command = new KickParticipantCommand(sessionId, Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        var act4 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act4.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ParticipantNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var session = CreateSessionWithPlayers(sessionId, ownerId, Guid.NewGuid());

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new KickParticipantCommand(sessionId, Guid.NewGuid(), ownerId);

        // Act & Assert
        var act5 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act5.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_KickHost_ThrowsConflictException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var session = CreateSessionWithPlayers(sessionId, ownerId, playerId);

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Try to kick the host
        var command = new KickParticipantCommand(sessionId, ownerId, ownerId);

        // Act & Assert
        var act6 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act6.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_FinalizedSession_ThrowsConflictException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var session = CreateSessionWithPlayers(sessionId, ownerId, playerId);

        // Finalize
        session.Finalize();

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new KickParticipantCommand(sessionId, playerId, ownerId);

        // Act & Assert
        var act7 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act7.Should().ThrowAsync<ConflictException>();
    }
}

// ============================================================================
// Issue #4765 – New player action handlers
// ============================================================================

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class UpdatePlayerScoreCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _mockSessionRepo = new();
    private readonly Mock<IScoreEntryRepository> _mockScoreRepo = new();
    private readonly Mock<IUnitOfWork> _mockUnitOfWork = new();
    private readonly Mock<ISessionSyncService> _mockSyncService = new();
    private readonly Mock<ISessionBroadcastService> _mockBroadcast = new();
    private readonly UpdatePlayerScoreCommandHandler _handler;

    public UpdatePlayerScoreCommandHandlerTests()
    {
        _handler = new UpdatePlayerScoreCommandHandler(
            _mockSessionRepo.Object,
            _mockScoreRepo.Object,
            _mockUnitOfWork.Object,
            _mockSyncService.Object,
            _mockBroadcast.Object);
    }

    private static Session CreateActiveSession(Guid sessionId, Guid participantId)
    {
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.GameSpecific);
        typeof(Session).GetProperty("Id")!.SetValue(session, sessionId);
        var field = typeof(Session).GetField("_participants",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = (List<Participant>)field!.GetValue(session)!;
        list.Add(new Participant { Id = participantId, SessionId = sessionId, DisplayName = "P1", Role = ParticipantRole.Player });
        return session;
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsResult()
    {
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        // Player scoring for themselves: requesterId == participantId
        var session = CreateActiveSession(sessionId, participantId);

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        _mockScoreRepo.Setup(r => r.GetByParticipantAsync(sessionId, participantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScoreEntry>());
        _mockScoreRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScoreEntry>());

        var command = new UpdatePlayerScoreCommand(sessionId, participantId, participantId, 10m, RoundNumber: 1);
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.ScoreEntryId.Should().NotBe(Guid.Empty);
        _mockScoreRepo.Verify(r => r.AddAsync(It.IsAny<ScoreEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockSyncService.Verify(s => s.PublishEventAsync(sessionId, It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PlayerUpdatesOtherParticipantScore_ThrowsForbiddenException()
    {
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var requesterId = Guid.NewGuid(); // different participant — Player role
        var session = CreateActiveSession(sessionId, participantId);
        // Add requester as a Player (not a Host)
        var field = typeof(Session).GetField("_participants",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = (List<Participant>)field!.GetValue(session)!;
        list.Add(new Participant { Id = requesterId, SessionId = sessionId, DisplayName = "Requester", Role = ParticipantRole.Player });

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var command = new UpdatePlayerScoreCommand(sessionId, participantId, requesterId, 5m, RoundNumber: 1);
        var act8 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act8.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        _mockSessionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);
        var command = new UpdatePlayerScoreCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5m);
        var act9 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act9.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_InactiveSession_ThrowsConflictException()
    {
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var session = CreateActiveSession(sessionId, participantId);
        session.Finalize();
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var command = new UpdatePlayerScoreCommand(sessionId, participantId, Guid.NewGuid(), 5m);
        var act10 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act10.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_ParticipantNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateActiveSession(sessionId, Guid.NewGuid());
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var command = new UpdatePlayerScoreCommand(sessionId, Guid.NewGuid(), Guid.NewGuid(), 5m);
        var act11 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act11.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ConcurrencyConflict_PublishesConflictEventAndThrows()
    {
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        // Player scoring for themselves: requesterId == participantId
        var session = CreateActiveSession(sessionId, participantId);

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Conflict"));

        var command = new UpdatePlayerScoreCommand(sessionId, participantId, participantId, 5m, RoundNumber: 1);
        var act12 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act12.Should().ThrowAsync<ConflictException>();

        _mockBroadcast.Verify(b => b.PublishAsync(
            sessionId,
            It.IsAny<INotification>(),
            It.Is<EventVisibility>(v => v.TargetUserId == participantId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class RollSessionDiceCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _mockSessionRepo = new();
    private readonly Mock<IDiceRollRepository> _mockDiceRepo = new();
    private readonly Mock<IUnitOfWork> _mockUnitOfWork = new();
    private readonly Mock<ISessionSyncService> _mockSyncService = new();
    private readonly RollSessionDiceCommandHandler _handler;

    public RollSessionDiceCommandHandlerTests()
    {
        _handler = new RollSessionDiceCommandHandler(
            _mockSessionRepo.Object,
            _mockDiceRepo.Object,
            _mockUnitOfWork.Object,
            _mockSyncService.Object);
    }

    private static Session CreateActiveSession(Guid sessionId, Guid participantId)
    {
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.GameSpecific);
        typeof(Session).GetProperty("Id")!.SetValue(session, sessionId);
        var field = typeof(Session).GetField("_participants",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = (List<Participant>)field!.GetValue(session)!;
        list.Add(new Participant { Id = participantId, SessionId = sessionId, DisplayName = "P1", Role = ParticipantRole.Player });
        return session;
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsRollResult()
    {
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var session = CreateActiveSession(sessionId, participantId);

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var command = new RollSessionDiceCommand(sessionId, participantId, Guid.NewGuid(), "2d6");
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.Formula.Should().Be("2D6");
        result.Rolls.Should().NotBeNull();
        _mockDiceRepo.Verify(r => r.AddAsync(It.IsAny<DiceRoll>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockSyncService.Verify(s => s.PublishEventAsync(sessionId, It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        _mockSessionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);
        var command = new RollSessionDiceCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "1d6");
        var act13 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act13.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_InactiveSession_ThrowsConflictException()
    {
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var session = CreateActiveSession(sessionId, participantId);
        session.Finalize();
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var command = new RollSessionDiceCommand(sessionId, participantId, Guid.NewGuid(), "1d6");
        var act14 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act14.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_ParticipantNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateActiveSession(sessionId, Guid.NewGuid());
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var command = new RollSessionDiceCommand(sessionId, Guid.NewGuid(), Guid.NewGuid(), "1d6");
        var act15 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act15.Should().ThrowAsync<NotFoundException>();
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class DrawSessionCardCommandHandlerTests
{
    private readonly Mock<ISessionDeckRepository> _mockDeckRepo = new();
    private readonly Mock<ISessionRepository> _mockSessionRepo = new();
    private readonly Mock<ISessionSyncService> _mockSyncService = new();
    private readonly DrawSessionCardCommandHandler _handler;

    public DrawSessionCardCommandHandlerTests()
    {
        _handler = new DrawSessionCardCommandHandler(
            _mockDeckRepo.Object,
            _mockSessionRepo.Object,
            _mockSyncService.Object);
    }

    private static Session CreateActiveSession(Guid sessionId, Guid participantId)
    {
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.GameSpecific);
        typeof(Session).GetProperty("Id")!.SetValue(session, sessionId);
        var field = typeof(Session).GetField("_participants",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = (List<Participant>)field!.GetValue(session)!;
        list.Add(new Participant { Id = participantId, SessionId = sessionId, DisplayName = "P1", Role = ParticipantRole.Player });
        return session;
    }

    [Fact]
    public async Task Handle_DeckNotFound_ThrowsNotFoundException()
    {
        _mockDeckRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionDeck?)null);
        var command = new DrawSessionCardCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var act16 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act16.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_DeckBelongsToDifferentSession_ThrowsForbiddenException()
    {
        var deckId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var deck = SessionDeck.CreateStandardDeck(Guid.NewGuid(), "Test Deck"); // different session
        typeof(SessionDeck).GetProperty("Id")!.SetValue(deck, deckId);

        _mockDeckRepo.Setup(r => r.GetByIdAsync(deckId, It.IsAny<CancellationToken>())).ReturnsAsync(deck);

        var command = new DrawSessionCardCommand(sessionId, deckId, Guid.NewGuid(), Guid.NewGuid());
        var act17 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act17.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        var deckId = Guid.NewGuid();
        var deck = SessionDeck.CreateStandardDeck(sessionId, "Test Deck");
        typeof(SessionDeck).GetProperty("Id")!.SetValue(deck, deckId);

        _mockDeckRepo.Setup(r => r.GetByIdAsync(deckId, It.IsAny<CancellationToken>())).ReturnsAsync(deck);
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync((Session?)null);

        var command = new DrawSessionCardCommand(sessionId, deckId, Guid.NewGuid(), Guid.NewGuid());
        var act18 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act18.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ParticipantNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        var deckId = Guid.NewGuid();
        var deck = SessionDeck.CreateStandardDeck(sessionId, "Test Deck");
        typeof(SessionDeck).GetProperty("Id")!.SetValue(deck, deckId);
        var session = CreateActiveSession(sessionId, Guid.NewGuid());

        _mockDeckRepo.Setup(r => r.GetByIdAsync(deckId, It.IsAny<CancellationToken>())).ReturnsAsync(deck);
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var command = new DrawSessionCardCommand(sessionId, deckId, Guid.NewGuid(), Guid.NewGuid());
        var act19 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act19.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_InactiveSession_ThrowsConflictException()
    {
        var sessionId = Guid.NewGuid();
        var deckId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var deck = SessionDeck.CreateStandardDeck(sessionId, "Test Deck");
        typeof(SessionDeck).GetProperty("Id")!.SetValue(deck, deckId);
        var session = CreateActiveSession(sessionId, participantId);
        session.Finalize();

        _mockDeckRepo.Setup(r => r.GetByIdAsync(deckId, It.IsAny<CancellationToken>())).ReturnsAsync(deck);
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var command = new DrawSessionCardCommand(sessionId, deckId, participantId, Guid.NewGuid());
        var act20 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act20.Should().ThrowAsync<ConflictException>();
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class SessionTimerActionCommandHandlerTests
{
    private readonly TimerStateManager _timerManager = new();
    private readonly Mock<ILogger<SessionTimerActionCommandHandler>> _mockLogger = new();
    private readonly SessionTimerActionCommandHandler _handler;

    public SessionTimerActionCommandHandlerTests()
    {
        _handler = new SessionTimerActionCommandHandler(_timerManager, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_StartAction_CreatesTimer()
    {
        var sessionId = Guid.NewGuid();
        var command = new SessionTimerActionCommand(sessionId, Guid.NewGuid(), Guid.NewGuid(), TimerAction.Start, "Player1", 60);
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);
        result.Action.Should().Be(TimerAction.Start);
        result.Status.Should().Be("running");
        result.RemainingSeconds.Should().Be(60);
    }

    [Fact]
    public async Task Handle_PauseAction_PausesRunningTimer()
    {
        var sessionId = Guid.NewGuid();
        _timerManager.CreateTimer(sessionId, 60, Guid.NewGuid(), "Player1");
        var command = new SessionTimerActionCommand(sessionId, Guid.NewGuid(), Guid.NewGuid(), TimerAction.Pause);
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);
        result.Action.Should().Be(TimerAction.Pause);
        result.Status.Should().Be("paused");
    }

    [Fact]
    public async Task Handle_PauseAction_NoRunningTimer_ThrowsConflictException()
    {
        var command = new SessionTimerActionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TimerAction.Pause);
        var act21 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act21.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_ResumeAction_ResumesPausedTimer()
    {
        var sessionId = Guid.NewGuid();
        var timer = _timerManager.CreateTimer(sessionId, 60, Guid.NewGuid(), "Player1");
        timer.Status = "paused";
        var command = new SessionTimerActionCommand(sessionId, Guid.NewGuid(), Guid.NewGuid(), TimerAction.Resume);
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);
        result.Action.Should().Be(TimerAction.Resume);
        result.Status.Should().Be("running");
    }

    [Fact]
    public async Task Handle_ResumeAction_NoPausedTimer_ThrowsConflictException()
    {
        var command = new SessionTimerActionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TimerAction.Resume);
        var act22 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act22.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_ResetAction_RemovesTimer()
    {
        var sessionId = Guid.NewGuid();
        _timerManager.CreateTimer(sessionId, 60, Guid.NewGuid(), "Player1");
        var command = new SessionTimerActionCommand(sessionId, Guid.NewGuid(), Guid.NewGuid(), TimerAction.Reset);
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);
        result.Action.Should().Be(TimerAction.Reset);
        result.Status.Should().Be("idle");
        _timerManager.GetTimer(sessionId).Should().BeNull();
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class SendChatActionCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _mockSessionRepo = new();
    private readonly Mock<ISessionChatRepository> _mockChatRepo = new();
    private readonly Mock<ISessionSyncService> _mockSyncService = new();
    private readonly SendChatActionCommandHandler _handler;

    public SendChatActionCommandHandlerTests()
    {
        _handler = new SendChatActionCommandHandler(
            _mockSessionRepo.Object,
            _mockChatRepo.Object,
            _mockSyncService.Object);
    }

    private static Session CreateActiveSession(Guid sessionId, Guid senderId)
    {
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.GameSpecific);
        typeof(Session).GetProperty("Id")!.SetValue(session, sessionId);
        var field = typeof(Session).GetField("_participants",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = (List<Participant>)field!.GetValue(session)!;
        list.Add(new Participant { Id = senderId, SessionId = sessionId, DisplayName = "Sender", Role = ParticipantRole.Spectator });
        return session;
    }

    [Fact]
    public async Task Handle_ValidCommand_SavesMessageAndPublishesEvent()
    {
        var sessionId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var session = CreateActiveSession(sessionId, senderId);

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        _mockChatRepo.Setup(r => r.GetNextSequenceNumberAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new SendChatActionCommand(sessionId, senderId, Guid.NewGuid(), "Hello World");
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.SequenceNumber.Should().Be(1);
        _mockChatRepo.Verify(r => r.AddAsync(It.IsAny<SessionChatMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockChatRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockSyncService.Verify(s => s.PublishEventAsync(sessionId, It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        _mockSessionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);
        var command = new SendChatActionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Hi");
        var act23 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act23.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_SenderNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateActiveSession(sessionId, Guid.NewGuid());
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var command = new SendChatActionCommand(sessionId, Guid.NewGuid(), Guid.NewGuid(), "Hi");
        var act24 = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act24.Should().ThrowAsync<NotFoundException>();
    }
}
