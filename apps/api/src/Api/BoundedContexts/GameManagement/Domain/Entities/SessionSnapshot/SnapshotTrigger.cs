namespace Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;

/// <summary>
/// Defines what triggered a session snapshot creation.
/// </summary>
internal enum SnapshotTrigger
{
    /// <summary>Automatic snapshot when a turn advances.</summary>
    TurnAdvanced = 0,

    /// <summary>Automatic snapshot when a game phase changes.</summary>
    PhaseAdvanced = 1,

    /// <summary>User-initiated manual save.</summary>
    ManualSave = 2,

    /// <summary>Triggered by a specific game event (Phase 3).</summary>
    EventTriggered = 3,

    /// <summary>Automatic snapshot when a score changes.</summary>
    ScoreChanged = 4,

    /// <summary>Automatic snapshot when a timer expires.</summary>
    TimerExpired = 5
}
