using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Application.Handlers.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.SessionSnapshot;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class SessionSnapshotCommandHandlerTests
{
    private readonly Mock<ISessionSnapshotRepository> _snapshotRepoMock;
    private readonly Mock<ILiveSessionRepository> _sessionRepoMock;
    private readonly Mock<IUnitOfWork> _uowMock;

    public SessionSnapshotCommandHandlerTests()
    {
        _snapshotRepoMock = new Mock<ISessionSnapshotRepository>();
        _sessionRepoMock = new Mock<ILiveSessionRepository>();
        _uowMock = new Mock<IUnitOfWork>();
    }

    // ========================================================================
    // CreateSnapshotCommandHandler
    // ========================================================================

    [Fact]
    public async Task CreateSnapshot_WhenSessionNotFound_ThrowsNotFoundException()
    {
        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var handler = new CreateSnapshotCommandHandler(
            _snapshotRepoMock.Object, _sessionRepoMock.Object, _uowMock.Object);
        var command = new CreateSnapshotCommand(Guid.NewGuid(), SnapshotTrigger.ManualSave, "Save", null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateSnapshot_FirstSnapshot_IsCheckpointWithFullState()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateLiveSession(sessionId, "{\"turn\":0}");

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _snapshotRepoMock.Setup(r => r.GetLatestBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot?)null);

        var handler = new CreateSnapshotCommandHandler(
            _snapshotRepoMock.Object, _sessionRepoMock.Object, _uowMock.Object);
        var command = new CreateSnapshotCommand(sessionId, SnapshotTrigger.ManualSave, "Initial", null);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.SnapshotIndex);
        Assert.True(result.IsCheckpoint);
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal(SnapshotTrigger.ManualSave, result.TriggerType);
        Assert.Equal("Initial", result.TriggerDescription);

        _snapshotRepoMock.Verify(r => r.AddAsync(
            It.Is<Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot>(
                s => s.IsCheckpoint && s.SnapshotIndex == 0),
            It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateSnapshot_SecondSnapshot_IsNotCheckpoint()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateLiveSession(sessionId, "{\"turn\":1,\"score\":10}");

        var existingSnapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), sessionId, 0, SnapshotTrigger.TurnAdvanced, null,
            "{\"turn\":0,\"score\":0}", true, 0, null, null);

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _snapshotRepoMock.Setup(r => r.GetLatestBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSnapshot);
        _snapshotRepoMock.Setup(r => r.GetSnapshotsForReconstructionAsync(sessionId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot> { existingSnapshot });

        var handler = new CreateSnapshotCommandHandler(
            _snapshotRepoMock.Object, _sessionRepoMock.Object, _uowMock.Object);
        var command = new CreateSnapshotCommand(sessionId, SnapshotTrigger.TurnAdvanced, null, null);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.SnapshotIndex);
        Assert.False(result.IsCheckpoint);
    }

    [Fact]
    public async Task CreateSnapshot_AtCheckpointInterval_IsCheckpoint()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateLiveSession(sessionId, "{\"turn\":10}");

        // Existing latest snapshot is at index 9
        var latestSnapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), sessionId, 9, SnapshotTrigger.TurnAdvanced, null,
            "[]", false, 9, null, null);

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _snapshotRepoMock.Setup(r => r.GetLatestBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestSnapshot);

        var handler = new CreateSnapshotCommandHandler(
            _snapshotRepoMock.Object, _sessionRepoMock.Object, _uowMock.Object);
        var command = new CreateSnapshotCommand(sessionId, SnapshotTrigger.TurnAdvanced, null, null);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Index 10 is a checkpoint (every 10)
        Assert.Equal(10, result.SnapshotIndex);
        Assert.True(result.IsCheckpoint);
    }

    [Fact]
    public async Task CreateSnapshot_WithNullGameState_UsesEmptyJson()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateLiveSession(sessionId, null);

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _snapshotRepoMock.Setup(r => r.GetLatestBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot?)null);

        var handler = new CreateSnapshotCommandHandler(
            _snapshotRepoMock.Object, _sessionRepoMock.Object, _uowMock.Object);
        var command = new CreateSnapshotCommand(sessionId, SnapshotTrigger.ManualSave, null, null);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.SnapshotIndex);
        Assert.True(result.IsCheckpoint);

        // Verify the snapshot was saved with empty JSON
        _snapshotRepoMock.Verify(r => r.AddAsync(
            It.Is<Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot>(
                s => s.DeltaDataJson == "{}"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateSnapshot_WithPlayerId_PassesPlayerIdToSnapshot()
    {
        var sessionId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var session = CreateLiveSession(sessionId, "{}");

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _snapshotRepoMock.Setup(r => r.GetLatestBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot?)null);

        var handler = new CreateSnapshotCommandHandler(
            _snapshotRepoMock.Object, _sessionRepoMock.Object, _uowMock.Object);
        var command = new CreateSnapshotCommand(sessionId, SnapshotTrigger.ManualSave, "Player save", playerId);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Equal(playerId, result.CreatedByPlayerId);
    }

    // ========================================================================
    // Helper
    // ========================================================================

    private static LiveGameSession CreateLiveSession(Guid sessionId, string? gameStateJson)
    {
        var userId = Guid.NewGuid();
        var session = LiveGameSession.Create(
            sessionId, userId, "Test Game");

        if (gameStateJson != null)
        {
            var gameState = JsonDocument.Parse(gameStateJson);
            session.UpdateGameState(gameState);
        }

        return session;
    }
}
