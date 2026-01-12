using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Domain event raised when a shared game is published.
/// </summary>
internal sealed class SharedGamePublishedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the published game.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// Gets the ID of the user who published the game.
    /// </summary>
    public Guid PublishedBy { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedGamePublishedEvent"/> class.
    /// </summary>
    public SharedGamePublishedEvent(Guid gameId, Guid publishedBy)
    {
        GameId = gameId;
        PublishedBy = publishedBy;
    }
}
