using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using FluentAssertions;

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

        session.PhaseNames.Length.Should().Be(4);
        session.PhaseNames[0].Should().Be("Draw");
        session.PhaseNames[3].Should().Be("Cleanup");
        session.CurrentPhaseIndex.Should().Be(0);
    }

    [Fact]
    public void ConfigurePhases_EmptyStringsFiltered_OnlyNonEmptyKept()
    {
        var session = CreateStartedSession();
        var phases = new[] { "Draw", "", "  ", "Action" };

        session.ConfigurePhases(phases, _timeProvider);

        session.PhaseNames.Length.Should().Be(2);
        session.PhaseNames[0].Should().Be("Draw");
        session.PhaseNames[1].Should().Be("Action");
    }

    [Fact]
    public void ConfigurePhases_TrimsWhitespace()
    {
        var session = CreateStartedSession();
        var phases = new[] { "  Draw  ", "  Action  " };

        session.ConfigurePhases(phases, _timeProvider);

        session.PhaseNames[0].Should().Be("Draw");
        session.PhaseNames[1].Should().Be("Action");
    }

    [Fact]
    public void ConfigurePhases_CompletedSession_ThrowsConflict()
    {
        var session = CreateStartedSession();
        session.Complete(_timeProvider);

        var act = () =>
            session.ConfigurePhases(new[] { "Draw" }, _timeProvider);
        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void ConfigurePhases_NullPhases_ThrowsArgumentNull()
    {
        var session = CreateStartedSession();

        var act = () =>
            session.ConfigurePhases(null!, _timeProvider);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region AdvancePhase

    [Fact]
    public void AdvancePhase_FirstPhaseToSecond_AdvancesCorrectly()
    {
        var session = CreateStartedSession();
        session.ConfigurePhases(new[] { "Draw", "Action", "Cleanup" }, _timeProvider);

        session.AdvancePhase(_timeProvider);

        session.CurrentPhaseIndex.Should().Be(1);
        session.GetCurrentPhaseName().Should().Be("Action");
    }

    [Fact]
    public void AdvancePhase_MultipleTimes_AdvancesThroughAllPhases()
    {
        var session = CreateStartedSession();
        session.ConfigurePhases(new[] { "Draw", "Action", "Buy", "Cleanup" }, _timeProvider);

        session.AdvancePhase(_timeProvider); // → Action (1)
        session.AdvancePhase(_timeProvider); // → Buy (2)
        session.AdvancePhase(_timeProvider); // → Cleanup (3)

        session.CurrentPhaseIndex.Should().Be(3);
        session.GetCurrentPhaseName().Should().Be("Cleanup");
    }

    [Fact]
    public void AdvancePhase_LastPhase_WrapsToZero()
    {
        var session = CreateStartedSession();
        session.ConfigurePhases(new[] { "Draw", "Action" }, _timeProvider);

        session.AdvancePhase(_timeProvider); // → Action (1)
        session.AdvancePhase(_timeProvider); // → Draw (0) - wrap

        session.CurrentPhaseIndex.Should().Be(0);
        session.GetCurrentPhaseName().Should().Be("Draw");
    }

    [Fact]
    public void AdvancePhase_NoPhases_ThrowsDomainException()
    {
        var session = CreateStartedSession();

        var act = () =>
            session.AdvancePhase(_timeProvider);
        var ex = act.Should().Throw<DomainException>().Which;

        ex.Message.Should().Contain("No phases configured");
    }

    [Fact]
    public void AdvancePhase_NotInProgress_ThrowsConflict()
    {
        var session = LiveGameSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Test", _timeProvider);
        session.AddPlayer(Guid.NewGuid(), "P1", PlayerColor.Red, _timeProvider);
        session.ConfigurePhases(new[] { "Draw" }, _timeProvider);
        // Session is Created, not InProgress

        var act = () =>
            session.AdvancePhase(_timeProvider);
        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void AdvancePhase_RaisesDomainEvent()
    {
        var session = CreateStartedSession();
        session.ConfigurePhases(new[] { "Draw", "Action" }, _timeProvider);
        session.ClearDomainEvents();

        session.AdvancePhase(_timeProvider);

        var events = session.DomainEvents.OfType<LiveSessionPhaseAdvancedEvent>().ToList();
        events.Should().ContainSingle();
        events[0].SessionId.Should().Be(session.Id);
        events[0].NewPhaseIndex.Should().Be(1);
        events[0].PhaseName.Should().Be("Action");
        events[0].TotalPhases.Should().Be(2);
    }

    [Fact]
    public void AdvancePhase_UpdatesTurnRecord()
    {
        var session = CreateStartedSession();
        session.ConfigurePhases(new[] { "Draw", "Action", "Cleanup" }, _timeProvider);
        session.AdvanceTurn(_timeProvider); // Creates a TurnRecord

        session.AdvancePhase(_timeProvider);

        var lastRecord = session.TurnRecords.LastOrDefault();
        lastRecord.Should().NotBeNull();
        lastRecord.PhaseIndex.Should().Be(1);
        lastRecord.PhaseName.Should().Be("Action");
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

        session.CurrentPhaseIndex.Should().Be(0);
    }

    [Fact]
    public void AdvanceTurn_NewTurnRecordHasPhaseZero()
    {
        var session = CreateStartedSession();
        session.ConfigurePhases(new[] { "Draw", "Action" }, _timeProvider);
        session.AdvancePhase(_timeProvider); // → Action (1)

        session.AdvanceTurn(_timeProvider);

        var lastRecord = session.TurnRecords.Last();
        lastRecord.PhaseIndex.Should().Be(0);
        lastRecord.PhaseName.Should().Be("Draw");
    }

    #endregion

    #region SnapshotTriggerConfig

    [Fact]
    public void SetSnapshotTriggerConfig_ValidConfig_SetsSuccessfully()
    {
        var session = CreateStartedSession();
        var config = SnapshotTriggerConfig.CreateDefault();

        session.SetSnapshotTriggerConfig(config, _timeProvider);

        session.SnapshotTriggerConfig.Should().NotBeNull();
        session.SnapshotTriggerConfig.Should().Be(config);
    }

    [Fact]
    public void SetSnapshotTriggerConfig_CompletedSession_ThrowsConflict()
    {
        var session = CreateStartedSession();
        session.Complete(_timeProvider);
        var config = SnapshotTriggerConfig.CreateDefault();

        var act = () =>
            session.SetSnapshotTriggerConfig(config, _timeProvider);
        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void SetSnapshotTriggerConfig_Null_ThrowsArgumentNull()
    {
        var session = CreateStartedSession();

        var act = () =>
            session.SetSnapshotTriggerConfig(null!, _timeProvider);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordSnapshotTimestamp_SetsTimestamp()
    {
        var session = CreateStartedSession();
        var timestamp = new DateTime(2026, 2, 19, 14, 30, 0, DateTimeKind.Utc);

        session.RecordSnapshotTimestamp(timestamp);

        session.LastSnapshotTimestamp.Should().Be(timestamp);
    }

    #endregion

    #region GetCurrentPhaseName

    [Fact]
    public void GetCurrentPhaseName_WithPhases_ReturnsCurrentName()
    {
        var session = CreateStartedSession();
        session.ConfigurePhases(new[] { "Draw", "Action" }, _timeProvider);

        session.GetCurrentPhaseName().Should().Be("Draw");
    }

    [Fact]
    public void GetCurrentPhaseName_NoPhases_ReturnsNull()
    {
        var session = CreateStartedSession();

        session.GetCurrentPhaseName().Should().BeNull();
    }

    #endregion
}
