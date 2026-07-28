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
public class UpdateScoreCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<IScoreEntryRepository> _scoreRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ISessionSyncService> _syncServiceMock = new();
    private readonly UpdateScoreCommandHandler _handler;

    public UpdateScoreCommandHandlerTests()
    {
        _handler = new UpdateScoreCommandHandler(
            _sessionRepoMock.Object, _scoreRepoMock.Object,
            _unitOfWorkMock.Object, _syncServiceMock.Object);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var command = new UpdateScoreCommand(Guid.NewGuid(), Guid.NewGuid(), 1, null, 10m, Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_FinalizedSession_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = Session.Create(userId, Guid.NewGuid(), SessionType.Generic);
        session.Finalize();

        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new UpdateScoreCommand(session.Id, session.Participants.First().Id, 1, null, 10m, userId);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ParticipantNotInSession_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = Session.Create(userId, Guid.NewGuid(), SessionType.Generic);
        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new UpdateScoreCommand(session.Id, Guid.NewGuid(), 1, null, 10m, userId);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CallerNotOwnerOrParticipant_ThrowsForbiddenException()
    {
        // Arrange — session owned by someone else; caller is neither owner nor participant (IDOR).
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var session = Session.Create(ownerId, Guid.NewGuid(), SessionType.Generic);

        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new UpdateScoreCommand(
            session.Id, session.Participants.First().Id, 1, null, 10m, attackerId);

        // Act & Assert — IDOR guard rejects before any score is persisted.
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.Handle(command, CancellationToken.None));
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
