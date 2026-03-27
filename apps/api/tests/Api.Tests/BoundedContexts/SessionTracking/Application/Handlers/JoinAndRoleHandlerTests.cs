using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

#region JoinSessionByCodeCommandHandler Tests

/// <summary>
/// Unit tests for JoinSessionByCodeCommandHandler.
/// Issue #4766 - Session Join via Code + Active Player Roles
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class JoinSessionByCodeCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _mockSessionRepo;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ISessionSyncService> _mockSyncService;
    private readonly JoinSessionByCodeCommandHandler _handler;

    public JoinSessionByCodeCommandHandlerTests()
    {
        _mockSessionRepo = new Mock<ISessionRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockSyncService = new Mock<ISessionSyncService>();
        var mockLogger = new Mock<ILogger<JoinSessionByCodeCommandHandler>>();
        _handler = new JoinSessionByCodeCommandHandler(
            _mockSessionRepo.Object,
            _mockUnitOfWork.Object,
            _mockSyncService.Object,
            mockLogger.Object);
    }

    private static Session CreateSessionWithCode(string code, Guid? sessionId = null)
    {
        var userId = Guid.NewGuid();
        var session = Session.Create(userId, Guid.NewGuid(), SessionType.GameSpecific);

        // Set session code via reflection
        typeof(Session).GetProperty("SessionCode")!.SetValue(session, code);
        if (sessionId.HasValue)
            typeof(Session).GetProperty("Id")!.SetValue(session, sessionId.Value);

        return session;
    }

    [Fact]
    public async Task Handle_ValidCode_JoinsSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionWithCode("ABC123", sessionId);
        var userId = Guid.NewGuid();

        _mockSessionRepo
            .Setup(r => r.GetByCodeAsync("ABC123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new JoinSessionByCodeCommand("abc123", userId, "New Player");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.SessionId.Should().Be(sessionId);
        result.SessionCode.Should().Be("ABC123");
        result.DisplayName.Should().Be("New Player");
        result.Role.Should().Be("Player");
        result.JoinOrder.Should().BeGreaterThan(0);

        _mockSessionRepo.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockSyncService.Verify(s => s.PublishEventAsync(sessionId, It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_LowercaseCode_NormalizesToUppercase()
    {
        // Arrange
        var session = CreateSessionWithCode("XYZ789");

        _mockSessionRepo
            .Setup(r => r.GetByCodeAsync("XYZ789", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new JoinSessionByCodeCommand("xyz789", Guid.NewGuid(), "Player");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - should have called with normalized uppercase
        _mockSessionRepo.Verify(r => r.GetByCodeAsync("XYZ789", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _mockSessionRepo
            .Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var command = new JoinSessionByCodeCommand("NOCODE", Guid.NewGuid(), "Player");

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*NOCODE*not found*");
    }

    [Fact]
    public async Task Handle_FinalizedSession_ThrowsConflictException()
    {
        // Arrange
        var session = CreateSessionWithCode("ABC123");
        session.Finalize();

        _mockSessionRepo
            .Setup(r => r.GetByCodeAsync("ABC123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new JoinSessionByCodeCommand("ABC123", Guid.NewGuid(), "Player");

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*finalized*");
    }

    [Fact]
    public async Task Handle_ExistingParticipant_ReturnsExistingInfo()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = CreateSessionWithCode("ABC123");

        // The session creator is already a participant - set their UserId
        var owner = session.Participants.First();
        typeof(Participant).GetProperty("UserId")!.SetValue(owner, (Guid?)userId);

        _mockSessionRepo
            .Setup(r => r.GetByCodeAsync("ABC123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new JoinSessionByCodeCommand("ABC123", userId, "Ignored Name");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - returns existing participant info, no update/save
        result.ParticipantId.Should().Be(owner.Id);
        result.DisplayName.Should().Be(owner.DisplayName);
        _mockSessionRepo.Verify(r => r.UpdateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

#endregion

#region AssignParticipantRoleCommandHandler Tests

/// <summary>
/// Unit tests for AssignParticipantRoleCommandHandler.
/// Issue #4766 - Session Join via Code + Active Player Roles
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class AssignParticipantRoleCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _mockSessionRepo;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ISessionSyncService> _mockSyncService;
    private readonly AssignParticipantRoleCommandHandler _handler;

    public AssignParticipantRoleCommandHandlerTests()
    {
        _mockSessionRepo = new Mock<ISessionRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockSyncService = new Mock<ISessionSyncService>();
        var mockLogger = new Mock<ILogger<AssignParticipantRoleCommandHandler>>();
        _handler = new AssignParticipantRoleCommandHandler(
            _mockSessionRepo.Object,
            _mockUnitOfWork.Object,
            _mockSyncService.Object,
            mockLogger.Object);
    }

    private static Session CreateSessionForRoleAssignment(
        out Guid hostUserId, out Guid playerParticipantId)
    {
        hostUserId = Guid.NewGuid();
        var session = Session.Create(hostUserId, Guid.NewGuid(), SessionType.GameSpecific);

        var owner = session.Participants.First();
        owner.Role = ParticipantRole.Host;

        // Add a player via reflection
        playerParticipantId = Guid.NewGuid();
        var participantsField = typeof(Session).GetField("_participants",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = (List<Participant>)participantsField!.GetValue(session)!;
        list.Add(new Participant
        {
            Id = playerParticipantId,
            SessionId = session.Id,
            UserId = Guid.NewGuid(),
            DisplayName = "Player 1",
            IsOwner = false,
            Role = ParticipantRole.Player,
            JoinOrder = 2
        });

        return session;
    }

    [Fact]
    public async Task Handle_ValidRoleChange_ReturnsResult()
    {
        // Arrange
        var session = CreateSessionForRoleAssignment(out var hostUserId, out var playerParticipantId);

        _mockSessionRepo
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AssignParticipantRoleCommand(
            session.Id, playerParticipantId, ParticipantRole.Spectator, hostUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ParticipantId.Should().Be(playerParticipantId);
        result.DisplayName.Should().Be("Player 1");
        result.PreviousRole.Should().Be("Player");
        result.NewRole.Should().Be("Spectator");

        _mockSessionRepo.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockSyncService.Verify(s => s.PublishEventAsync(session.Id, It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _mockSessionRepo
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var command = new AssignParticipantRoleCommand(
            sessionId, Guid.NewGuid(), ParticipantRole.Host, Guid.NewGuid());

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{sessionId}*not found*");
    }

    [Fact]
    public async Task Handle_ParticipantNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var session = CreateSessionForRoleAssignment(out var hostUserId, out _);
        var unknownParticipantId = Guid.NewGuid();

        _mockSessionRepo
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AssignParticipantRoleCommand(
            session.Id, unknownParticipantId, ParticipantRole.Host, hostUserId);

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{unknownParticipantId}*not found*");
    }

    [Fact]
    public async Task Handle_PromoteToHost_PublishesSseEvent()
    {
        // Arrange
        var session = CreateSessionForRoleAssignment(out var hostUserId, out var playerParticipantId);

        _mockSessionRepo
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AssignParticipantRoleCommand(
            session.Id, playerParticipantId, ParticipantRole.Host, hostUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.PreviousRole.Should().Be("Player");
        result.NewRole.Should().Be("Host");
        _mockSyncService.Verify(
            s => s.PublishEventAsync(session.Id, It.IsAny<INotification>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

#endregion
