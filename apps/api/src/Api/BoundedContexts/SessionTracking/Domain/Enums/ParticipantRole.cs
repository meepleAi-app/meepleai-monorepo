namespace Api.BoundedContexts.SessionTracking.Domain.Enums;

/// <summary>
/// Defines the role of a participant in a game session.
/// Determines which actions the participant can perform.
/// Issue #4765 - Player Action Endpoints + Host Validation
/// </summary>
public enum ParticipantRole
{
    /// <summary>
    /// View-only access. Can send chat messages but cannot modify session state.
    /// </summary>
    Spectator = 0,

    /// <summary>
    /// Active participant. Can update own score, roll dice, draw cards, use timer, send chat.
    /// </summary>
    Player = 1,

    /// <summary>
    /// Session host (owner). Can perform all player actions plus:
    /// advance turns, pause/resume session, kick participants, modify toolkit.
    /// </summary>
    Host = 2
}
