using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.UserLibrary.Domain.Events;

/// <summary>
/// Domain event raised when a game's state changes in the user's library.
/// </summary>
internal sealed class GameStateChangedEvent : DomainEventBase
{
    /// <summary>
    /// The ID of the library entry that changed state.
    /// </summary>
    public Guid LibraryEntryId { get; }

    /// <summary>
    /// The user who owns the library entry.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// The game whose state changed.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// The previous state of the game.
    /// </summary>
    public GameStateType? PreviousState { get; }

    /// <summary>
    /// The new state of the game.
    /// </summary>
    public GameStateType NewState { get; }

    public GameStateChangedEvent(
        Guid libraryEntryId,
        Guid userId,
        Guid gameId,
        GameStateType? previousState,
        GameStateType newState,
        DateTime occurredAt)
    {
        LibraryEntryId = libraryEntryId;
        UserId = userId;
        GameId = gameId;
        PreviousState = previousState;
        NewState = newState;
    }
}
