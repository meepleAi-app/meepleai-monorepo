using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class KickParticipantCommandHandlerAdditionalTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ISessionSyncService> _syncServiceMock = new();
    private readonly KickParticipantCommandHandler _handler;

    public KickParticipantCommandHandlerAdditionalTests()
    {
        _handler = new KickParticipantCommandHandler(
            _sessionRepoMock.Object,
            _unitOfWorkMock.Object,
            _syncServiceMock.Object);
    }

    [Fact]
    public void Constructor_NullSessionRepository_ThrowsArgumentNullException()
    {
        var act = () => new KickParticipantCommandHandler(
            null!,
            _unitOfWorkMock.Object,
            _syncServiceMock.Object);

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_NullUnitOfWork_ThrowsArgumentNullException()
    {
        var act = () => new KickParticipantCommandHandler(
            _sessionRepoMock.Object,
            null!,
            _syncServiceMock.Object);

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_NullSyncService_ThrowsArgumentNullException()
    {
        var act = () => new KickParticipantCommandHandler(
            _sessionRepoMock.Object,
            _unitOfWorkMock.Object,
            null!);

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var command = new KickParticipantCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ParticipantNotInSession_ThrowsNotFoundException()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new KickParticipantCommand(session.Id, Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidKick_RemovesParticipantAndPublishesEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = Session.Create(userId, Guid.NewGuid(), SessionType.Generic);

        // Add a second participant (non-host) to kick
        session.AddParticipant(ParticipantInfo.Create("Player2", false, 2), Guid.NewGuid());
        var participantToKick = session.Participants.First(p => !p.IsOwner);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _syncServiceMock
            .Setup(s => s.PublishEventAsync(It.IsAny<Guid>(), It.IsAny<MediatR.INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new KickParticipantCommand(session.Id, participantToKick.Id, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(participantToKick.Id, result.ParticipantId);
        Assert.Equal("Player2", result.DisplayName);

        _sessionRepoMock.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _syncServiceMock.Verify(s => s.PublishEventAsync(session.Id, It.IsAny<MediatR.INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
