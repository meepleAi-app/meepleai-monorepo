using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Tests for turn phase advancement and snapshot trigger configuration on LiveGameSession.
/// Issue #4761: Turn Phases from TurnTemplate + Event-Triggered Snapshots.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class LiveGameSessionPhaseTests
{
    private readonly FakeTimeProvider _timeProvider;

    public LiveGameSessionPhaseTests()
    {
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 2, 19, 14, 0, 0, TimeSpan.Zero));
    }

    private LiveGameSession CreateStartedSession()
    {
        var session = LiveGameSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", _timeProvider);
        session.AddPlayer(Guid.NewGuid(), "Player 1", PlayerColor.Red, _timeProvider);
        session.Start(_timeProvider);
        return session;
    }

    #region ConfigurePhases

    [Fact]
    public void ConfigurePhases_ValidPhases_SetsPhaseNamesAndResetsIndex()
    {
        var session = CreateStartedSession();
        var phases = new[] { "Draw", "Action", "Buy", "Cleanup" };

        session.ConfigurePhases(phases, _timeProvider);

        Assert.Equal(4, session.PhaseNames.Length);
        Assert.Equal("Draw", session.PhaseNames[0]);
        Assert.Equal("Cleanup", session.PhaseNames[3]);
        Assert.Equal(0, session.CurrentPhaseIndex);
    }

    [Fact]
    public void ConfigurePhases_EmptyStringsFiltered_OnlyNonEmptyKept()
    {
        var session = CreateStartedSession();
        var phases = new[] { "Draw", "", "  ", "Action" };

        session.ConfigurePhases(phases, _timeProvider);

        Assert.Equal(2, session.PhaseNames.Length);
        Assert.Equal("Draw", session.PhaseNames[0]);
        Assert.Equal("Action", session.PhaseNames[1]);
    }

    [Fact]
    public void ConfigurePhases_TrimsWhitespace()
    {
        var session = CreateStartedSession();
        var phases = new[] { "  Draw  ", "  Action  " };

        session.ConfigurePhases(phases, _timeProvider);

        Assert.Equal("Draw", session.PhaseNames[0]);
        Assert.Equal("Action", session.PhaseNames[1]);
    }

    [Fact]
    public void ConfigurePhases_CompletedSession_ThrowsConflict()
    {
        var session = CreateStartedSession();
        session.Complete(_timeProvider);

        Assert.Throws<ConflictException>(() =>
            session.ConfigurePhases(new[] { "Draw" }, _timeProvider));
    }

    [Fact]
    public void ConfigurePhases_NullPhases_ThrowsArgumentNull()
    {
        var session = CreateStartedSession();

        Assert.Throws<ArgumentNullException>(() =>
            session.ConfigurePhases(null!, _timeProvider));
    }

    #endregion

    #region AdvancePhase

    [Fact]
    public void AdvancePhase_FirstPhaseToSecond_AdvancesCorrectly()
    {
        var session = CreateStartedSession();
        session.ConfigurePhases(new[] { "Draw", "Action", "Cleanup" }, _timeProvider);

        session.AdvancePhase(_timeProvider);

        Assert.Equal(1, session.CurrentPhaseIndex);
        Assert.Equal("Action", session.GetCurrentPhaseName());
    }

    [Fact]
    public void AdvancePhase_MultipleTimes_AdvancesThroughAllPhases()
    {
        var session = CreateStartedSession();
        session.ConfigurePhases(new[] { "Draw", "Action", "Buy", "Cleanup" }, _timeProvider);

        session.AdvancePhase(_timeProvider); // → Action (1)
        session.AdvancePhase(_timeProvider); // → Buy (2)
        session.AdvancePhase(_timeProvider); // → Cleanup (3)

        Assert.Equal(3, session.CurrentPhaseIndex);
        Assert.Equal("Cleanup", session.GetCurrentPhaseName());
    }

    [Fact]
    public void AdvancePhase_LastPhase_WrapsToZero()
    {
        var session = CreateStartedSession();
        session.ConfigurePhases(new[] { "Draw", "Action" }, _timeProvider);

        session.AdvancePhase(_timeProvider); // → Action (1)
        session.AdvancePhase(_timeProvider); // → Draw (0) - wrap

        Assert.Equal(0, session.CurrentPhaseIndex);
        Assert.Equal("Draw", session.GetCurrentPhaseName());
    }

    [Fact]
    public void AdvancePhase_NoPhases_ThrowsDomainException()
    {
        var session = CreateStartedSession();

        var ex = Assert.Throws<DomainException>(() =>
            session.AdvancePhase(_timeProvider));

        Assert.Contains("No phases configured", ex.Message);
    }

    [Fact]
    public void AdvancePhase_NotInProgress_ThrowsConflict()
    {
        var session = LiveGameSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Test", _timeProvider);
        session.AddPlayer(Guid.NewGuid(), "P1", PlayerColor.Red, _timeProvider);
        session.ConfigurePhases(new[] { "Draw" }, _timeProvider);
        // Session is Created, not InProgress

        Assert.Throws<ConflictException>(() =>
            session.AdvancePhase(_timeProvider));
    }

    [Fact]
    public void AdvancePhase_RaisesDomainEvent()
    {
        var session = CreateStartedSession();
        session.ConfigurePhases(new[] { "Draw", "Action" }, _timeProvider);
        session.ClearDomainEvents();

        session.AdvancePhase(_timeProvider);

        var events = session.DomainEvents.OfType<LiveSessionPhaseAdvancedEvent>().ToList();
        Assert.Single(events);
        Assert.Equal(session.Id, events[0].SessionId);
        Assert.Equal(1, events[0].NewPhaseIndex);
        Assert.Equal("Action", events[0].PhaseName);
        Assert.Equal(2, events[0].TotalPhases);
    }

    [Fact]
    public void AdvancePhase_UpdatesTurnRecord()
    {
        var session = CreateStartedSession();
        session.ConfigurePhases(new[] { "Draw", "Action", "Cleanup" }, _timeProvider);
        session.AdvanceTurn(_timeProvider); // Creates a TurnRecord

        session.AdvancePhase(_timeProvider);

        var lastRecord = session.TurnRecords.LastOrDefault();
        Assert.NotNull(lastRecord);
        Assert.Equal(1, lastRecord.PhaseIndex);
        Assert.Equal("Action", lastRecord.PhaseName);
    }

    #endregion

    #region AdvanceTurn Resets Phase

    [Fact]
    public void AdvanceTurn_ResetsPhaseToZero()
    {
        var session = CreateStartedSession();
        session.ConfigurePhases(new[] { "Draw", "Action", "Cleanup" }, _timeProvider);
        session.AdvancePhase(_timeProvider); // → Action (1)
        session.AdvancePhase(_timeProvider); // → Cleanup (2)

        session.AdvanceTurn(_timeProvider);

        Assert.Equal(0, session.CurrentPhaseIndex);
    }

    [Fact]
    public void AdvanceTurn_NewTurnRecordHasPhaseZero()
    {
        var session = CreateStartedSession();
        session.ConfigurePhases(new[] { "Draw", "Action" }, _timeProvider);
        session.AdvancePhase(_timeProvider); // → Action (1)

        session.AdvanceTurn(_timeProvider);

        var lastRecord = session.TurnRecords.Last();
        Assert.Equal(0, lastRecord.PhaseIndex);
        Assert.Equal("Draw", lastRecord.PhaseName);
    }

    #endregion

    #region SnapshotTriggerConfig

    [Fact]
    public void SetSnapshotTriggerConfig_ValidConfig_SetsSuccessfully()
    {
        var session = CreateStartedSession();
        var config = SnapshotTriggerConfig.CreateDefault();

        session.SetSnapshotTriggerConfig(config, _timeProvider);

        Assert.NotNull(session.SnapshotTriggerConfig);
        Assert.Equal(config, session.SnapshotTriggerConfig);
    }

    [Fact]
    public void SetSnapshotTriggerConfig_CompletedSession_ThrowsConflict()
    {
        var session = CreateStartedSession();
        session.Complete(_timeProvider);
        var config = SnapshotTriggerConfig.CreateDefault();

        Assert.Throws<ConflictException>(() =>
            session.SetSnapshotTriggerConfig(config, _timeProvider));
    }

    [Fact]
    public void SetSnapshotTriggerConfig_Null_ThrowsArgumentNull()
    {
        var session = CreateStartedSession();

        Assert.Throws<ArgumentNullException>(() =>
            session.SetSnapshotTriggerConfig(null!, _timeProvider));
    }

    [Fact]
    public void RecordSnapshotTimestamp_SetsTimestamp()
    {
        var session = CreateStartedSession();
        var timestamp = new DateTime(2026, 2, 19, 14, 30, 0, DateTimeKind.Utc);

        session.RecordSnapshotTimestamp(timestamp);

        Assert.Equal(timestamp, session.LastSnapshotTimestamp);
    }

    #endregion

    #region GetCurrentPhaseName

    [Fact]
    public void GetCurrentPhaseName_WithPhases_ReturnsCurrentName()
    {
        var session = CreateStartedSession();
        session.ConfigurePhases(new[] { "Draw", "Action" }, _timeProvider);

        Assert.Equal("Draw", session.GetCurrentPhaseName());
    }

    [Fact]
    public void GetCurrentPhaseName_NoPhases_ReturnsNull()
    {
        var session = CreateStartedSession();

        Assert.Null(session.GetCurrentPhaseName());
    }

    #endregion
}
