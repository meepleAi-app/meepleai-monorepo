using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class RestoreSessionCheckpointCommandHandlerTests : IDisposable
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ISessionCheckpointRepository> _checkpointRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly MeepleAiDbContext _db;
    private readonly RestoreSessionCheckpointCommandHandler _handler;

    public RestoreSessionCheckpointCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _handler = new RestoreSessionCheckpointCommandHandler(
            _sessionRepoMock.Object,
            _checkpointRepoMock.Object,
            _unitOfWorkMock.Object,
            _db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_NullSessionRepository_ThrowsArgumentNullException()
    {
        var act = () => new RestoreSessionCheckpointCommandHandler(
            null!,
            _checkpointRepoMock.Object,
            _unitOfWorkMock.Object,
            _db);

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_NullCheckpointRepository_ThrowsArgumentNullException()
    {
        var act = () => new RestoreSessionCheckpointCommandHandler(
            _sessionRepoMock.Object,
            null!,
            _unitOfWorkMock.Object,
            _db);

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_NullUnitOfWork_ThrowsArgumentNullException()
    {
        var act = () => new RestoreSessionCheckpointCommandHandler(
            _sessionRepoMock.Object,
            _checkpointRepoMock.Object,
            null!,
            _db);

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_NullDbContext_ThrowsArgumentNullException()
    {
        var act = () => new RestoreSessionCheckpointCommandHandler(
            _sessionRepoMock.Object,
            _checkpointRepoMock.Object,
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

        var command = new RestoreSessionCheckpointCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SessionNotActive_ThrowsConflictException()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        session.Finalize();

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new RestoreSessionCheckpointCommand(session.Id, Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CheckpointNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _checkpointRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionCheckpoint?)null);

        var command = new RestoreSessionCheckpointCommand(session.Id, Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CheckpointBelongsToDifferentSession_ThrowsConflictException()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        var checkpoint = SessionCheckpoint.Create(
            Guid.NewGuid(), // different session
            "My Checkpoint",
            Guid.NewGuid(),
            "[]",
            0);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _checkpointRepoMock
            .Setup(r => r.GetByIdAsync(checkpoint.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(checkpoint);

        var command = new RestoreSessionCheckpointCommand(session.Id, Guid.NewGuid(), checkpoint.Id);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidRestore_ReturnsResultWithWidgetsRestored()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        var snapshotData = "[]"; // empty snapshots → 0 widgets restored
        var checkpoint = SessionCheckpoint.Create(
            session.Id,
            "Checkpoint Alpha",
            session.UserId,
            snapshotData,
            0);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _checkpointRepoMock
            .Setup(r => r.GetByIdAsync(checkpoint.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(checkpoint);

        var command = new RestoreSessionCheckpointCommand(session.Id, session.UserId, checkpoint.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(checkpoint.Id, result.CheckpointId);
        Assert.Equal("Checkpoint Alpha", result.Name);
        Assert.Equal(0, result.WidgetsRestored);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
