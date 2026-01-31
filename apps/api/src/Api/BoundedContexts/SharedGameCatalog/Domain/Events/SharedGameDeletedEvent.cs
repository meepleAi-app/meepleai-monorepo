using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Domain event raised when a shared game is deleted.
/// </summary>
internal sealed class SharedGameDeletedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the deleted game.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// Gets the ID of the user who deleted the game.
    /// </summary>
    public Guid DeletedBy { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedGameDeletedEvent"/> class.
    /// </summary>
    /// <param name="gameId">The game ID</param>
    /// <param name="deletedBy">The user ID who deleted the game</param>
    public SharedGameDeletedEvent(Guid gameId, Guid deletedBy)
    {
        GameId = gameId;
        DeletedBy = deletedBy;
    }
}
