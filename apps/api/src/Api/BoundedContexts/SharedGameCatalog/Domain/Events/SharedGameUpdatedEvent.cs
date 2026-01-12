using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Domain event raised when a shared game is updated.
/// </summary>
internal sealed class SharedGameUpdatedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the updated game.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// Gets the ID of the user who modified the game.
    /// </summary>
    public Guid ModifiedBy { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedGameUpdatedEvent"/> class.
    /// </summary>
    public SharedGameUpdatedEvent(Guid gameId, Guid modifiedBy)
    {
        GameId = gameId;
        ModifiedBy = modifiedBy;
    }
}
