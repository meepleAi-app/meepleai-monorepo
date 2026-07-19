using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

#region GenerateInviteTokenCommandHandler Tests

/// <summary>
/// Unit tests for GenerateInviteTokenCommandHandler.
/// Issue #3170 - the handler staged the invite token via SessionRepository.UpdateAsync
/// (stage-only) but never called SaveChangesAsync, so the token was never persisted.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class GenerateInviteTokenCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _mockSessionRepo;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly GenerateInviteTokenCommandHandler _handler;

    public GenerateInviteTokenCommandHandlerTests()
    {
        _mockSessionRepo = new Mock<ISessionRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockConfig = new Mock<IConfiguration>();
        var mockLogger = new Mock<ILogger<GenerateInviteTokenCommandHandler>>();
        _handler = new GenerateInviteTokenCommandHandler(
            _mockSessionRepo.Object,
            _mockUnitOfWork.Object,
            mockConfig.Object,
            mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsInviteTokenViaSaveChanges()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var session = Session.Create(ownerId, Guid.NewGuid(), SessionType.GameSpecific);
        typeof(Session).GetProperty("Id")!.SetValue(session, sessionId);

        _mockSessionRepo
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new GenerateInviteTokenCommand(sessionId, ownerId, 24);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - the invite token must be persisted, not merely staged
        result.InviteToken.Should().NotBeNullOrEmpty();
        _mockSessionRepo.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

#endregion

#region JoinSessionByInviteCommandHandler Tests

/// <summary>
/// Unit tests for JoinSessionByInviteCommandHandler.
/// Issue #3170 - the handler staged the new participant via SessionRepository.UpdateAsync
/// (stage-only) but never called SaveChangesAsync, so the join was never persisted.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class JoinSessionByInviteCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _mockSessionRepo;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly JoinSessionByInviteCommandHandler _handler;

    public JoinSessionByInviteCommandHandlerTests()
    {
        _mockSessionRepo = new Mock<ISessionRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockLogger = new Mock<ILogger<JoinSessionByInviteCommandHandler>>();
        _handler = new JoinSessionByInviteCommandHandler(
            _mockSessionRepo.Object,
            _mockUnitOfWork.Object,
            mockLogger.Object);
    }

    [Fact]
    public async Task Handle_NewParticipant_PersistsJoinViaSaveChanges()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var session = Session.Create(ownerId, Guid.NewGuid(), SessionType.GameSpecific);
        var token = session.GenerateInviteToken(24);

        _mockSessionRepo
            .Setup(r => r.GetByInviteTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new JoinSessionByInviteCommand(token, Guid.NewGuid(), "New Player");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - the new participant must be persisted, not merely staged
        result.ParticipantId.Should().NotBeEmpty();
        _mockSessionRepo.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingParticipant_DoesNotSave()
    {
        // Arrange - the owner (already a participant) re-joins via invite: no mutation, no save
        var ownerId = Guid.NewGuid();
        var session = Session.Create(ownerId, Guid.NewGuid(), SessionType.GameSpecific);
        var owner = session.Participants.First();
        typeof(Participant).GetProperty("UserId")!.SetValue(owner, (Guid?)ownerId);
        var token = session.GenerateInviteToken(24);

        _mockSessionRepo
            .Setup(r => r.GetByInviteTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new JoinSessionByInviteCommand(token, ownerId, "Ignored");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockSessionRepo.Verify(r => r.UpdateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

#endregion
