using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Domain event raised when a shared game is quick-published (Draft → Published directly).
/// Issue #250: Quick-publish endpoint for admin shared games
/// </summary>
internal sealed class SharedGameQuickPublishedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the game that was quick-published.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// Gets the ID of the admin who quick-published the game.
    /// </summary>
    public Guid PublishedBy { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedGameQuickPublishedEvent"/> class.
    /// </summary>
    public SharedGameQuickPublishedEvent(Guid gameId, Guid publishedBy)
    {
        GameId = gameId;
        PublishedBy = publishedBy;
    }
}
