using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Domain event raised when a shared game is archived.
/// </summary>
internal sealed class SharedGameArchivedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the archived game.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// Gets the ID of the user who archived the game.
    /// </summary>
    public Guid ArchivedBy { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedGameArchivedEvent"/> class.
    /// </summary>
    public SharedGameArchivedEvent(Guid gameId, Guid archivedBy)
    {
        GameId = gameId;
        ArchivedBy = archivedBy;
    }
}
