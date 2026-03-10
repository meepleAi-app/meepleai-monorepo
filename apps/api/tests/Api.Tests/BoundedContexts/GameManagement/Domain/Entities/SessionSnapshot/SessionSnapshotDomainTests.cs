using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class SessionSnapshotDomainTests
{
    private readonly Guid _sessionId = Guid.NewGuid();

    [Fact]
    public void Constructor_WithValidArgs_CreatesSnapshot()
    {
        var id = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var snapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            id, _sessionId, 0, SnapshotTrigger.ManualSave, "Initial save",
            "{\"score\":10}", true, 1, 2, playerId);

        Assert.Equal(id, snapshot.Id);
        Assert.Equal(_sessionId, snapshot.SessionId);
        Assert.Equal(0, snapshot.SnapshotIndex);
        Assert.Equal(SnapshotTrigger.ManualSave, snapshot.TriggerType);
        Assert.Equal("Initial save", snapshot.TriggerDescription);
        Assert.Equal("{\"score\":10}", snapshot.DeltaDataJson);
        Assert.True(snapshot.IsCheckpoint);
        Assert.Equal(1, snapshot.TurnIndex);
        Assert.Equal(2, snapshot.PhaseIndex);
        Assert.Equal(playerId, snapshot.CreatedByPlayerId);
        Assert.True(snapshot.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public void Constructor_WithNullDeltaData_DefaultsToEmptyJson()
    {
        var snapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.TurnAdvanced, null,
            "", false, 0, null, null);

        Assert.Equal("{}", snapshot.DeltaDataJson);
    }

    [Fact]
    public void Constructor_TrimsDescription()
    {
        var snapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.ManualSave, "  My Save  ",
            "{}", true, 0, null, null);

        Assert.Equal("My Save", snapshot.TriggerDescription);
    }

    [Fact]
    public void Constructor_WithNullDescription_AllowsNull()
    {
        var snapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.TurnAdvanced, null,
            "{}", true, 0, null, null);

        Assert.Null(snapshot.TriggerDescription);
    }

    [Fact]
    public void Constructor_WithEmptySessionId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
                Guid.NewGuid(), Guid.Empty, 0, SnapshotTrigger.TurnAdvanced, null,
                "{}", true, 0, null, null));
    }

    [Fact]
    public void Constructor_WithNegativeSnapshotIndex_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
                Guid.NewGuid(), _sessionId, -1, SnapshotTrigger.TurnAdvanced, null,
                "{}", true, 0, null, null));
    }

    [Fact]
    public void Constructor_WithNegativeTurnIndex_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
                Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.TurnAdvanced, null,
                "{}", true, -1, null, null));
    }

    [Fact]
    public void Constructor_WithNullPhaseIndex_AllowsNull()
    {
        var snapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.TurnAdvanced, null,
            "{}", true, 0, null, null);

        Assert.Null(snapshot.PhaseIndex);
    }

    [Fact]
    public void Constructor_WithNullPlayerId_AllowsNull()
    {
        var snapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.EventTriggered, null,
            "{}", true, 0, null, null);

        Assert.Null(snapshot.CreatedByPlayerId);
    }

    // === ShouldBeCheckpoint ===

    [Fact]
    public void ShouldBeCheckpoint_Index0_ReturnsTrue()
    {
        Assert.True(Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.ShouldBeCheckpoint(0));
    }

    [Fact]
    public void ShouldBeCheckpoint_Index10_ReturnsTrue()
    {
        Assert.True(Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.ShouldBeCheckpoint(10));
    }

    [Fact]
    public void ShouldBeCheckpoint_Index20_ReturnsTrue()
    {
        Assert.True(Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.ShouldBeCheckpoint(20));
    }

    [Fact]
    public void ShouldBeCheckpoint_Index1_ReturnsFalse()
    {
        Assert.False(Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.ShouldBeCheckpoint(1));
    }

    [Fact]
    public void ShouldBeCheckpoint_Index9_ReturnsFalse()
    {
        Assert.False(Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.ShouldBeCheckpoint(9));
    }

    [Fact]
    public void ShouldBeCheckpoint_Index15_ReturnsFalse()
    {
        Assert.False(Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.ShouldBeCheckpoint(15));
    }

    // === GetNearestCheckpointIndex ===

    [Fact]
    public void GetNearestCheckpointIndex_Index0_Returns0()
    {
        Assert.Equal(0,
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.GetNearestCheckpointIndex(0));
    }

    [Fact]
    public void GetNearestCheckpointIndex_Index5_Returns0()
    {
        Assert.Equal(0,
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.GetNearestCheckpointIndex(5));
    }

    [Fact]
    public void GetNearestCheckpointIndex_Index10_Returns10()
    {
        Assert.Equal(10,
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.GetNearestCheckpointIndex(10));
    }

    [Fact]
    public void GetNearestCheckpointIndex_Index15_Returns10()
    {
        Assert.Equal(10,
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.GetNearestCheckpointIndex(15));
    }

    [Fact]
    public void GetNearestCheckpointIndex_Index25_Returns20()
    {
        Assert.Equal(20,
            Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.GetNearestCheckpointIndex(25));
    }

    // === SnapshotTrigger enum values ===

    [Fact]
    public void Constructor_WithTurnAdvancedTrigger_Succeeds()
    {
        var snapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.TurnAdvanced, null, "{}", true, 0, null, null);
        Assert.Equal(SnapshotTrigger.TurnAdvanced, snapshot.TriggerType);
    }

    [Fact]
    public void Constructor_WithPhaseAdvancedTrigger_Succeeds()
    {
        var snapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.PhaseAdvanced, null, "{}", true, 0, null, null);
        Assert.Equal(SnapshotTrigger.PhaseAdvanced, snapshot.TriggerType);
    }

    [Fact]
    public void Constructor_WithEventTriggeredTrigger_Succeeds()
    {
        var snapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.EventTriggered, "Event X", "{}", true, 0, null, null);
        Assert.Equal(SnapshotTrigger.EventTriggered, snapshot.TriggerType);
    }

    // === Issue #5581: SessionPaused and PreRestore trigger types ===

    [Fact]
    public void Constructor_WithSessionPausedTrigger_Succeeds()
    {
        var snapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.SessionPaused,
            "Auto \u2014 Pausa turno 5", "{}", true, 5, 0, null);
        Assert.Equal(SnapshotTrigger.SessionPaused, snapshot.TriggerType);
        Assert.Equal("Auto \u2014 Pausa turno 5", snapshot.TriggerDescription);
    }

    [Fact]
    public void Constructor_WithPreRestoreTrigger_Succeeds()
    {
        var snapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.PreRestore,
            "Auto \u2014 Pre-restore turno 3", "{}", true, 3, 1, null);
        Assert.Equal(SnapshotTrigger.PreRestore, snapshot.TriggerType);
        Assert.Equal("Auto \u2014 Pre-restore turno 3", snapshot.TriggerDescription);
    }

    [Fact]
    public void SnapshotTrigger_SessionPaused_HasCorrectValue()
    {
        Assert.Equal(6, (int)SnapshotTrigger.SessionPaused);
    }

    [Fact]
    public void SnapshotTrigger_PreRestore_HasCorrectValue()
    {
        Assert.Equal(7, (int)SnapshotTrigger.PreRestore);
    }
}
