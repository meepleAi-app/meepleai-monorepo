using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Unit tests for <see cref="CreateLiveSessionCommandHandler"/> companion-saga wiring (ADR-083 SP0).
///
/// Verifies the atomic Saga contract introduced in Issue #2501 SP0:
///   - When <c>command.GameId</c> is present, the handler creates a SessionTracking.Session
///     companion via <see cref="ICompanionSessionService"/> BEFORE building the LiveGameSession,
///     populates <c>LiveGameSession.TrackingSessionId</c> with the companion id, and commits both
///     aggregates in a single <see cref="IUnitOfWork.SaveChangesAsync"/> (one EF transaction →
///     no orphan LiveGameSession is ever persisted if the companion insert fails).
///   - When <c>command.GameId</c> is null, no companion is created and TrackingSessionId stays null.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class CreateLiveSessionCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenCompanionFails_DoesNotPersistLiveSession()
    {
        var sessionRepo = new Mock<ILiveSessionRepository>();
        var uow = new Mock<IUnitOfWork>();
        var companion = new Mock<ICompanionSessionService>();
        companion.Setup(c => c.CreateCompanionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("companion insert failed"));

        var sut = new CreateLiveSessionCommandHandler(sessionRepo.Object, TimeProvider.System, uow.Object, companion.Object);
        var cmd = new CreateLiveSessionCommand(UserId: Guid.NewGuid(), GameName: "Mage Knight", GameId: Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Handle(cmd, CancellationToken.None));

        sessionRepo.Verify(r => r.AddAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithGameId_SetsTrackingSessionIdToCompanionId()
    {
        var trackingId = Guid.NewGuid();
        LiveGameSession? added = null;
        var sessionRepo = new Mock<ILiveSessionRepository>();
        sessionRepo.Setup(r => r.AddAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()))
            .Callback<LiveGameSession, CancellationToken>((s, _) => added = s)
            .Returns(Task.CompletedTask);
        var uow = new Mock<IUnitOfWork>();
        var companion = new Mock<ICompanionSessionService>();
        companion.Setup(c => c.CreateCompanionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trackingId);

        var sut = new CreateLiveSessionCommandHandler(sessionRepo.Object, TimeProvider.System, uow.Object, companion.Object);
        var cmd = new CreateLiveSessionCommand(UserId: Guid.NewGuid(), GameName: "Mage Knight", GameId: Guid.NewGuid());

        await sut.Handle(cmd, CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal(trackingId, added!.TrackingSessionId);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutGameId_DoesNotCreateCompanion()
    {
        LiveGameSession? added = null;
        var sessionRepo = new Mock<ILiveSessionRepository>();
        sessionRepo.Setup(r => r.AddAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()))
            .Callback<LiveGameSession, CancellationToken>((s, _) => added = s)
            .Returns(Task.CompletedTask);
        var uow = new Mock<IUnitOfWork>();
        var companion = new Mock<ICompanionSessionService>();

        var sut = new CreateLiveSessionCommandHandler(sessionRepo.Object, TimeProvider.System, uow.Object, companion.Object);
        var cmd = new CreateLiveSessionCommand(UserId: Guid.NewGuid(), GameName: "Free session", GameId: null);

        await sut.Handle(cmd, CancellationToken.None);

        Assert.NotNull(added);
        Assert.Null(added!.TrackingSessionId);
        companion.Verify(c => c.CreateCompanionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
