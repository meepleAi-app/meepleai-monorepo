using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.UserLibrary.Domain.Events;

/// <summary>
/// Domain event raised when a game is removed from a user's library.
/// </summary>
internal sealed class GameRemovedFromLibraryEvent : DomainEventBase
{
    /// <summary>
    /// The ID of the library entry that was removed.
    /// </summary>
    public Guid EntryId { get; }

    /// <summary>
    /// The ID of the user who removed the game.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// The ID of the game that was removed.
    /// </summary>
    public Guid GameId { get; }

    public GameRemovedFromLibraryEvent(Guid entryId, Guid userId, Guid gameId)
    {
        EntryId = entryId;
        UserId = userId;
        GameId = gameId;
    }
}
