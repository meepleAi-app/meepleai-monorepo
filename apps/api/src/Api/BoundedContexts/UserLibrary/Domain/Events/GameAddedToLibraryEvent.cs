using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.UserLibrary.Domain.Events;

/// <summary>
/// Domain event raised when a game is added to a user's library.
/// </summary>
internal sealed class GameAddedToLibraryEvent : DomainEventBase
{
    /// <summary>
    /// The ID of the library entry created.
    /// </summary>
    public Guid EntryId { get; }

    /// <summary>
    /// The ID of the user who added the game.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// The ID of the game that was added.
    /// </summary>
    public Guid GameId { get; }

    public GameAddedToLibraryEvent(Guid entryId, Guid userId, Guid gameId)
    {
        EntryId = entryId;
        UserId = userId;
        GameId = gameId;
    }
}
