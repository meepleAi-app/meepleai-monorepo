namespace Api.BoundedContexts.SessionTracking.Domain.Enums;

/// <summary>
/// Defines the role of a participant in a game session.
/// Determines which actions the participant can perform.
/// Issue #4765 - Player Action Endpoints + Host Validation
/// </summary>
/// <remarks>
/// LOAD-BEARING NUMERIC ORDERING — DO NOT REORDER (issue #3392).
/// The values are privilege-ascending (Spectator=0 &lt; Player=1 &lt; Host=2) and are compared
/// ORDINALLY in <see cref="Api.BoundedContexts.SessionTracking.Application.Behaviors.ValidatePlayerRoleBehavior{TRequest,TResponse}"/>
/// (<c>participant.Role &lt; request.MinimumRole</c>). Reordering or renumbering these members
/// would silently invert authorization checks. Note this is the OPPOSITE ordering of the
/// GameManagement <see cref="Api.BoundedContexts.GameManagement.Domain.Entities.SessionParticipantRole"/>
/// enum (Host=0), which is only ever compared with equality — the two must never be conflated.
/// </remarks>
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
