using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

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

        snapshot.Id.Should().Be(id);
        snapshot.SessionId.Should().Be(_sessionId);
        snapshot.SnapshotIndex.Should().Be(0);
        snapshot.TriggerType.Should().Be(SnapshotTrigger.ManualSave);
        snapshot.TriggerDescription.Should().Be("Initial save");
        snapshot.DeltaDataJson.Should().Be("{\"score\":10}");
        (snapshot.IsCheckpoint).Should().BeTrue();
        snapshot.TurnIndex.Should().Be(1);
        snapshot.PhaseIndex.Should().Be(2);
        snapshot.CreatedByPlayerId.Should().Be(playerId);
        (snapshot.Timestamp <= DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNullDeltaData_DefaultsToEmptyJson()
    {
        var snapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.TurnAdvanced, null,
            "", false, 0, null, null);

        snapshot.DeltaDataJson.Should().Be("{}");
    }

    [Fact]
    public void Constructor_TrimsDescription()
    {
        var snapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.ManualSave, "  My Save  ",
            "{}", true, 0, null, null);

        snapshot.TriggerDescription.Should().Be("My Save");
    }

    [Fact]
    public void Constructor_WithNullDescription_AllowsNull()
    {
        var snapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.TurnAdvanced, null,
            "{}", true, 0, null, null);

        snapshot.TriggerDescription.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithEmptySessionId_ThrowsArgumentException()
    {
        var act = () =>
            new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
                Guid.NewGuid(), Guid.Empty, 0, SnapshotTrigger.TurnAdvanced, null,
                "{}", true, 0, null, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNegativeSnapshotIndex_ThrowsArgumentException()
    {
        var act = () =>
            new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
                Guid.NewGuid(), _sessionId, -1, SnapshotTrigger.TurnAdvanced, null,
                "{}", true, 0, null, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNegativeTurnIndex_ThrowsArgumentException()
    {
        var act = () =>
            new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
                Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.TurnAdvanced, null,
                "{}", true, -1, null, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullPhaseIndex_AllowsNull()
    {
        var snapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.TurnAdvanced, null,
            "{}", true, 0, null, null);

        snapshot.PhaseIndex.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullPlayerId_AllowsNull()
    {
        var snapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.EventTriggered, null,
            "{}", true, 0, null, null);

        snapshot.CreatedByPlayerId.Should().BeNull();
    }

    // === ShouldBeCheckpoint ===

    [Fact]
    public void ShouldBeCheckpoint_Index0_ReturnsTrue()
    {
        (Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.ShouldBeCheckpoint(0)).Should().BeTrue();
    }

    [Fact]
    public void ShouldBeCheckpoint_Index10_ReturnsTrue()
    {
        (Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.ShouldBeCheckpoint(10)).Should().BeTrue();
    }

    [Fact]
    public void ShouldBeCheckpoint_Index20_ReturnsTrue()
    {
        (Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.ShouldBeCheckpoint(20)).Should().BeTrue();
    }

    [Fact]
    public void ShouldBeCheckpoint_Index1_ReturnsFalse()
    {
        (Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.ShouldBeCheckpoint(1)).Should().BeFalse();
    }

    [Fact]
    public void ShouldBeCheckpoint_Index9_ReturnsFalse()
    {
        (Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.ShouldBeCheckpoint(9)).Should().BeFalse();
    }

    [Fact]
    public void ShouldBeCheckpoint_Index15_ReturnsFalse()
    {
        (Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.ShouldBeCheckpoint(15)).Should().BeFalse();
    }

    // === GetNearestCheckpointIndex ===

    [Fact]
    public void GetNearestCheckpointIndex_Index0_Returns0()
    {
        Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.GetNearestCheckpointIndex(0).Should().Be(0);
    }

    [Fact]
    public void GetNearestCheckpointIndex_Index5_Returns0()
    {
        Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.GetNearestCheckpointIndex(5).Should().Be(0);
    }

    [Fact]
    public void GetNearestCheckpointIndex_Index10_Returns10()
    {
        Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.GetNearestCheckpointIndex(10).Should().Be(10);
    }

    [Fact]
    public void GetNearestCheckpointIndex_Index15_Returns10()
    {
        Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.GetNearestCheckpointIndex(15).Should().Be(10);
    }

    [Fact]
    public void GetNearestCheckpointIndex_Index25_Returns20()
    {
        Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot.GetNearestCheckpointIndex(25).Should().Be(20);
    }

    // === SnapshotTrigger enum values ===

    [Fact]
    public void Constructor_WithTurnAdvancedTrigger_Succeeds()
    {
        var snapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.TurnAdvanced, null, "{}", true, 0, null, null);
        snapshot.TriggerType.Should().Be(SnapshotTrigger.TurnAdvanced);
    }

    [Fact]
    public void Constructor_WithPhaseAdvancedTrigger_Succeeds()
    {
        var snapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.PhaseAdvanced, null, "{}", true, 0, null, null);
        snapshot.TriggerType.Should().Be(SnapshotTrigger.PhaseAdvanced);
    }

    [Fact]
    public void Constructor_WithEventTriggeredTrigger_Succeeds()
    {
        var snapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.EventTriggered, "Event X", "{}", true, 0, null, null);
        snapshot.TriggerType.Should().Be(SnapshotTrigger.EventTriggered);
    }

    // === Issue #5581: SessionPaused and PreRestore trigger types ===

    [Fact]
    public void Constructor_WithSessionPausedTrigger_Succeeds()
    {
        var snapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.SessionPaused,
            "Auto \u2014 Pausa turno 5", "{}", true, 5, 0, null);
        snapshot.TriggerType.Should().Be(SnapshotTrigger.SessionPaused);
        snapshot.TriggerDescription.Should().Be("Auto \u2014 Pausa turno 5");
    }

    [Fact]
    public void Constructor_WithPreRestoreTrigger_Succeeds()
    {
        var snapshot = new Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(), _sessionId, 0, SnapshotTrigger.PreRestore,
            "Auto \u2014 Pre-restore turno 3", "{}", true, 3, 1, null);
        snapshot.TriggerType.Should().Be(SnapshotTrigger.PreRestore);
        snapshot.TriggerDescription.Should().Be("Auto \u2014 Pre-restore turno 3");
    }

    [Fact]
    public void SnapshotTrigger_SessionPaused_HasCorrectValue()
    {
        ((int)SnapshotTrigger.SessionPaused).Should().Be(6);
    }

    [Fact]
    public void SnapshotTrigger_PreRestore_HasCorrectValue()
    {
        ((int)SnapshotTrigger.PreRestore).Should().Be(7);
    }
}
