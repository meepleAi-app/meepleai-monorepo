using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Handlers;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
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
        Assert.True(result.IsReady);
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
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
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
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
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
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
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
        Assert.Equal(playerId, result.ParticipantId);
        Assert.Equal("Player", result.DisplayName);
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
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
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
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
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
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
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
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }
}
