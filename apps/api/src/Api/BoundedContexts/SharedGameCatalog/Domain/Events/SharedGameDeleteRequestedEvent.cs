using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Domain event raised when a delete request is created for a shared game.
/// </summary>
internal sealed class SharedGameDeleteRequestedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the game being requested for deletion.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// Gets the ID of the delete request.
    /// </summary>
    public Guid RequestId { get; }

    /// <summary>
    /// Gets the ID of the user who requested the deletion.
    /// </summary>
    public Guid RequestedBy { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedGameDeleteRequestedEvent"/> class.
    /// </summary>
    /// <param name="gameId">The game ID</param>
    /// <param name="requestId">The delete request ID</param>
    /// <param name="requestedBy">The user ID who requested deletion</param>
    public SharedGameDeleteRequestedEvent(Guid gameId, Guid requestId, Guid requestedBy)
    {
        GameId = gameId;
        RequestId = requestId;
        RequestedBy = requestedBy;
    }
}
