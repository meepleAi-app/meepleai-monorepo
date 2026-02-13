namespace Api.BoundedContexts.UserLibrary.Domain.Enums;

/// <summary>
/// Supported entity types for generic user collections.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
public enum EntityType
{
    /// <summary>
    /// Game entity (handled by UserLibraryEntry for backward compatibility).
    /// </summary>
    Game = 0,

    /// <summary>
    /// Player entity (user following/friend).
    /// </summary>
    Player = 1,

    /// <summary>
    /// Event entity (user interested/attending).
    /// </summary>
    Event = 2,

    /// <summary>
    /// Game session entity (saved session).
    /// </summary>
    Session = 3,

    /// <summary>
    /// AI agent entity (favorite agent).
    /// </summary>
    Agent = 4,

    /// <summary>
    /// Document entity (saved document).
    /// </summary>
    Document = 5,

    /// <summary>
    /// Chat session entity (pinned chat).
    /// </summary>
    ChatSession = 6
}
