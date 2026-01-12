using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Domain event raised when a new shared game is created in the catalog.
/// </summary>
internal sealed class SharedGameCreatedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the created game.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// Gets the title of the created game.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the ID of the user who created the game.
    /// </summary>
    public Guid CreatedBy { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedGameCreatedEvent"/> class.
    /// </summary>
    public SharedGameCreatedEvent(Guid gameId, string title, Guid createdBy)
    {
        GameId = gameId;
        Title = title;
        CreatedBy = createdBy;
    }
}
