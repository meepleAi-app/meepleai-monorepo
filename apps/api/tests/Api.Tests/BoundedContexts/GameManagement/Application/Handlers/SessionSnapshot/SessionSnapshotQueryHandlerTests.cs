using Api.BoundedContexts.GameManagement.Application.Handlers.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Application.Queries.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.SessionSnapshot;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class SessionSnapshotQueryHandlerTests
{
    private readonly Mock<ISessionSnapshotRepository> _snapshotRepoMock;

    public SessionSnapshotQueryHandlerTests()
    {
        _snapshotRepoMock = new Mock<ISessionSnapshotRepository>();
    }

    // ========================================================================
    // GetSnapshotsQueryHandler
    // ========================================================================

    [Fact]
    public async Task GetSnapshots_WhenNoSnapshots_ReturnsEmptyList()
    {
        var sessionId = Guid.NewGuid();
        _snapshotRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot>());

        var handler = new GetSnapshotsQueryHandler(_snapshotRepoMock.Object);
        var query = new GetSnapshotsQuery(sessionId);

        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSnapshots_WithMultipleSnapshots_ReturnsMappedDtos()
    {
        var sessionId = Guid.NewGuid();
        var snapshots = new List<Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot>
        {
            CreateSnapshot(sessionId, 0, true, SnapshotTrigger.ManualSave),
            CreateSnapshot(sessionId, 1, false, SnapshotTrigger.TurnAdvanced),
            CreateSnapshot(sessionId, 2, false, SnapshotTrigger.TurnAdvanced),
        };

        _snapshotRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots);

        var handler = new GetSnapshotsQueryHandler(_snapshotRepoMock.Object);
        var query = new GetSnapshotsQuery(sessionId);

        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Count);
        Assert.Equal(0, result[0].SnapshotIndex);
        Assert.True(result[0].IsCheckpoint);
        Assert.Equal(1, result[1].SnapshotIndex);
        Assert.False(result[1].IsCheckpoint);
        Assert.Equal(2, result[2].SnapshotIndex);
    }

    [Fact]
    public async Task GetSnapshots_CorrectlyMapsTriggerType()
    {
        var sessionId = Guid.NewGuid();
        var snapshots = new List<Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot>
        {
            CreateSnapshot(sessionId, 0, true, SnapshotTrigger.ManualSave, "Manual save"),
        };

        _snapshotRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots);

        var handler = new GetSnapshotsQueryHandler(_snapshotRepoMock.Object);
        var result = await handler.Handle(new GetSnapshotsQuery(sessionId), TestContext.Current.CancellationToken);

        Assert.Equal(SnapshotTrigger.ManualSave, result[0].TriggerType);
        Assert.Equal("Manual save", result[0].TriggerDescription);
    }

    // ========================================================================
    // GetSnapshotStateQueryHandler
    // ========================================================================

    [Fact]
    public async Task GetSnapshotState_WhenNoSnapshots_ReturnsNull()
    {
        var sessionId = Guid.NewGuid();
        _snapshotRepoMock.Setup(r => r.GetSnapshotsForReconstructionAsync(sessionId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot>());

        var handler = new GetSnapshotStateQueryHandler(_snapshotRepoMock.Object);
        var query = new GetSnapshotStateQuery(sessionId, 0);

        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSnapshotState_CheckpointOnly_ReturnsFullState()
    {
        var sessionId = Guid.NewGuid();
        var checkpoint = CreateSnapshot(sessionId, 0, true, SnapshotTrigger.ManualSave,
            deltaJson: "{\"turn\":0,\"score\":0}");

        _snapshotRepoMock.Setup(r => r.GetSnapshotsForReconstructionAsync(sessionId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot> { checkpoint });

        var handler = new GetSnapshotStateQueryHandler(_snapshotRepoMock.Object);
        var query = new GetSnapshotStateQuery(sessionId, 0);

        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(0, result.SnapshotIndex);
        Assert.Equal(0, result.State.GetProperty("turn").GetInt32());
        Assert.Equal(0, result.State.GetProperty("score").GetInt32());
    }

    [Fact]
    public async Task GetSnapshotState_WithDeltas_ReconstructsCorrectly()
    {
        var sessionId = Guid.NewGuid();
        var checkpoint = CreateSnapshot(sessionId, 0, true, SnapshotTrigger.ManualSave,
            deltaJson: "{\"turn\":0,\"score\":0}");

        var delta1 = CreateSnapshot(sessionId, 1, false, SnapshotTrigger.TurnAdvanced,
            deltaJson: "[{\"op\":\"replace\",\"path\":\"/turn\",\"value\":1},{\"op\":\"replace\",\"path\":\"/score\",\"value\":10}]");

        var delta2 = CreateSnapshot(sessionId, 2, false, SnapshotTrigger.TurnAdvanced,
            deltaJson: "[{\"op\":\"replace\",\"path\":\"/turn\",\"value\":2},{\"op\":\"replace\",\"path\":\"/score\",\"value\":25}]");

        _snapshotRepoMock.Setup(r => r.GetSnapshotsForReconstructionAsync(sessionId, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot>
            {
                checkpoint, delta1, delta2
            });

        var handler = new GetSnapshotStateQueryHandler(_snapshotRepoMock.Object);
        var query = new GetSnapshotStateQuery(sessionId, 2);

        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(2, result.SnapshotIndex);
        Assert.Equal(2, result.State.GetProperty("turn").GetInt32());
        Assert.Equal(25, result.State.GetProperty("score").GetInt32());
    }

    [Fact]
    public async Task GetSnapshotState_WhenTargetSnapshotNotInList_ReturnsNull()
    {
        var sessionId = Guid.NewGuid();
        // Reconstruction returns snapshots but target (index 5) is missing
        var checkpoint = CreateSnapshot(sessionId, 0, true, SnapshotTrigger.ManualSave,
            deltaJson: "{\"turn\":0}");

        _snapshotRepoMock.Setup(r => r.GetSnapshotsForReconstructionAsync(sessionId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot> { checkpoint });

        var handler = new GetSnapshotStateQueryHandler(_snapshotRepoMock.Object);
        var query = new GetSnapshotStateQuery(sessionId, 5);

        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSnapshotState_WhenFirstIsNotCheckpoint_ReturnsNull()
    {
        var sessionId = Guid.NewGuid();
        // First snapshot is NOT a checkpoint (corrupt data scenario)
        var badSnapshot = CreateSnapshot(sessionId, 1, false, SnapshotTrigger.TurnAdvanced,
            deltaJson: "[{\"op\":\"replace\",\"path\":\"/x\",\"value\":1}]");

        _snapshotRepoMock.Setup(r => r.GetSnapshotsForReconstructionAsync(sessionId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot> { badSnapshot });

        var handler = new GetSnapshotStateQueryHandler(_snapshotRepoMock.Object);
        var query = new GetSnapshotStateQuery(sessionId, 1);

        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSnapshotState_IncludesMetadata()
    {
        var sessionId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var checkpoint = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), sessionId, 0, SnapshotTrigger.ManualSave, "My save",
            "{\"data\":1}", true, 3, 2, playerId);

        _snapshotRepoMock.Setup(r => r.GetSnapshotsForReconstructionAsync(sessionId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot> { checkpoint });

        var handler = new GetSnapshotStateQueryHandler(_snapshotRepoMock.Object);
        var result = await handler.Handle(new GetSnapshotStateQuery(sessionId, 0), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(0, result.SnapshotIndex);
        Assert.Equal(3, result.TurnIndex);
        Assert.Equal(2, result.PhaseIndex);
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private static Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot CreateSnapshot(
        Guid sessionId, int index, bool isCheckpoint, SnapshotTrigger trigger,
        string? description = null, string deltaJson = "{}")
    {
        return new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), sessionId, index, trigger, description,
            deltaJson, isCheckpoint, index, null, null);
    }
}
