namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Enum representing the current phase or stage of a game session.
/// Issue #2403: Game Session State Tracking.
/// </summary>
internal enum GamePhase
{
    /// <summary>
    /// Initial setup phase (placing pieces, drawing cards).
    /// </summary>
    Setup = 0,

    /// <summary>
    /// Main gameplay phase.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Scoring or endgame phase.
    /// </summary>
    Scoring = 2,

    /// <summary>
    /// Game completed.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Custom phase (game-specific).
    /// </summary>
    Custom = 99
}
