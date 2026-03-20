using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class AddParticipantCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ISessionSyncService> _syncServiceMock = new();
    private readonly AddParticipantCommandHandler _handler;

    public AddParticipantCommandHandlerTests()
    {
        _handler = new AddParticipantCommandHandler(
            _sessionRepoMock.Object, _unitOfWorkMock.Object, _syncServiceMock.Object);
    }

    [Fact]
    public async Task Handle_ValidRequest_AddsParticipantAndReturnsResult()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AddParticipantCommand(session.Id, "Alice", Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result.ParticipantId);
        Assert.Equal(2, result.JoinOrder);
        _sessionRepoMock.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var command = new AddParticipantCommand(sessionId, "Bob", null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_FinalizedSession_ThrowsConflictException()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        session.Finalize();

        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AddParticipantCommand(session.Id, "Charlie", null);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
