using Api.BoundedContexts.SessionTracking.Application.Behaviors;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Behaviors;

/// <summary>
/// Unit tests for ValidatePlayerRoleBehavior MediatR pipeline behavior.
/// Issue #4765 - Player Action Endpoints + Host Validation
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class ValidatePlayerRoleBehaviorTests
{
    private readonly Mock<ISessionRepository> _mockSessionRepo;
    private readonly ValidatePlayerRoleBehavior<MarkPlayerReadyCommand, MarkPlayerReadyResult> _playerBehavior;
    private readonly ValidatePlayerRoleBehavior<KickParticipantCommand, KickParticipantResult> _hostBehavior;

    public ValidatePlayerRoleBehaviorTests()
    {
        _mockSessionRepo = new Mock<ISessionRepository>();

        _playerBehavior = new ValidatePlayerRoleBehavior<MarkPlayerReadyCommand, MarkPlayerReadyResult>(
            _mockSessionRepo.Object,
            NullLogger<ValidatePlayerRoleBehavior<MarkPlayerReadyCommand, MarkPlayerReadyResult>>.Instance);

        _hostBehavior = new ValidatePlayerRoleBehavior<KickParticipantCommand, KickParticipantResult>(
            _mockSessionRepo.Object,
            NullLogger<ValidatePlayerRoleBehavior<KickParticipantCommand, KickParticipantResult>>.Instance);
    }

    private static Session CreateSessionWithUser(Guid sessionId, Guid userId, ParticipantRole role)
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
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = userId,
            DisplayName = "Test User",
            IsOwner = role == ParticipantRole.Host,
            Role = role
        });

        return session;
    }

    [Fact]
    public async Task Handle_PlayerWithPlayerRole_MarkReady_ShouldProceed()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var session = CreateSessionWithUser(sessionId, userId, ParticipantRole.Player);

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new MarkPlayerReadyCommand(sessionId, Guid.NewGuid(), userId);
        var expectedResult = new MarkPlayerReadyResult(true, 1, 2);
        RequestHandlerDelegate<MarkPlayerReadyResult> next = (ct) => Task.FromResult(expectedResult);

        // Act
        var result = await _playerBehavior.Handle(command, next, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task Handle_HostWithHostRole_KickParticipant_ShouldProceed()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var session = CreateSessionWithUser(sessionId, userId, ParticipantRole.Host);

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new KickParticipantCommand(sessionId, Guid.NewGuid(), userId);
        var expectedResult = new KickParticipantResult(Guid.NewGuid(), "Kicked Player");
        RequestHandlerDelegate<KickParticipantResult> next = (ct) => Task.FromResult(expectedResult);

        // Act
        var result = await _hostBehavior.Handle(command, next, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task Handle_PlayerTryingHostAction_ShouldThrowConflictException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var session = CreateSessionWithUser(sessionId, userId, ParticipantRole.Player);

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new KickParticipantCommand(sessionId, Guid.NewGuid(), userId);
        RequestHandlerDelegate<KickParticipantResult> next = (ct) =>
            Task.FromResult(new KickParticipantResult(Guid.NewGuid(), "Should not reach"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _hostBehavior.Handle(command, next, TestContext.Current.CancellationToken));
        ex.Message.Should().Contain("Insufficient role");
        ex.Message.Should().Contain("Host");
        ex.Message.Should().Contain("Player");
    }

    [Fact]
    public async Task Handle_SpectatorTryingPlayerAction_ShouldThrowConflictException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var session = CreateSessionWithUser(sessionId, userId, ParticipantRole.Spectator);

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new MarkPlayerReadyCommand(sessionId, Guid.NewGuid(), userId);
        RequestHandlerDelegate<MarkPlayerReadyResult> next = (ct) =>
            Task.FromResult(new MarkPlayerReadyResult(true, 1, 1));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _playerBehavior.Handle(command, next, TestContext.Current.CancellationToken));
        ex.Message.Should().Contain("Insufficient role");
    }

    [Fact]
    public async Task Handle_SessionNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var command = new MarkPlayerReadyCommand(sessionId, Guid.NewGuid(), Guid.NewGuid());
        RequestHandlerDelegate<MarkPlayerReadyResult> next = (ct) =>
            Task.FromResult(new MarkPlayerReadyResult(true, 1, 1));

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _playerBehavior.Handle(command, next, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_UserNotParticipant_ShouldThrowNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var nonParticipantUserId = Guid.NewGuid();
        var session = CreateSessionWithUser(sessionId, userId, ParticipantRole.Player);

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new MarkPlayerReadyCommand(sessionId, Guid.NewGuid(), nonParticipantUserId);
        RequestHandlerDelegate<MarkPlayerReadyResult> next = (ct) =>
            Task.FromResult(new MarkPlayerReadyResult(true, 1, 1));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _playerBehavior.Handle(command, next, TestContext.Current.CancellationToken));
        ex.Message.Should().Contain("not a participant");
    }

    [Fact]
    public async Task Handle_HostWithPlayerRole_ShouldProceed()
    {
        // Arrange - Host has higher role, should pass Player-level check
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var session = CreateSessionWithUser(sessionId, userId, ParticipantRole.Host);

        _mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new MarkPlayerReadyCommand(sessionId, Guid.NewGuid(), userId);
        var expectedResult = new MarkPlayerReadyResult(true, 1, 2);
        RequestHandlerDelegate<MarkPlayerReadyResult> next = (ct) => Task.FromResult(expectedResult);

        // Act
        var result = await _playerBehavior.Handle(command, next, TestContext.Current.CancellationToken);

        // Assert - Host passes Player-level check
        result.Should().Be(expectedResult);
    }
}
