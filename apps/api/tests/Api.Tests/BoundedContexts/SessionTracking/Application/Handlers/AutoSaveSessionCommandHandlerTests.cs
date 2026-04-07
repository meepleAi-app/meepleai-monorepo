using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class AutoSaveSessionCommandHandlerTests
{
    private readonly Mock<ISessionCheckpointRepository> _checkpointRepoMock = new();
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AutoSaveSessionCommandHandler _handler;

    public AutoSaveSessionCommandHandlerTests()
    {
        _handler = new AutoSaveSessionCommandHandler(
            _checkpointRepoMock.Object,
            _sessionRepoMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ActiveSession_CreatesCheckpoint()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        await _handler.Handle(new AutoSaveSessionCommand(session.Id), CancellationToken.None);

        // Assert
        _checkpointRepoMock.Verify(
            r => r.AddAsync(It.Is<SessionCheckpoint>(c =>
                c.SessionId == session.Id &&
                c.Name.StartsWith("Auto-save")),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SessionNotFound_SkipsSilently()
    {
        // Arrange
        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        // Act
        await _handler.Handle(new AutoSaveSessionCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        _checkpointRepoMock.Verify(
            r => r.AddAsync(It.IsAny<SessionCheckpoint>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_FinalizedSession_SkipsSilently()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        session.Finalize(); // Status becomes Finalized
        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        await _handler.Handle(new AutoSaveSessionCommand(session.Id), CancellationToken.None);

        // Assert
        _checkpointRepoMock.Verify(
            r => r.AddAsync(It.IsAny<SessionCheckpoint>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_PausedSession_SkipsSilently()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        session.Pause(); // Status becomes Paused
        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        await _handler.Handle(new AutoSaveSessionCommand(session.Id), CancellationToken.None);

        // Assert
        _checkpointRepoMock.Verify(
            r => r.AddAsync(It.IsAny<SessionCheckpoint>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
