using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Raised when a Game loses its link to a SharedGameCatalog entry.
/// Triggered either by admin action or because the SharedGame was deleted.
/// Spec-panel recommendation M-3.
/// </summary>
internal sealed class GameUnlinkedFromSharedCatalogEvent : DomainEventBase
{
    /// <summary>Gets the ID of the Game that was unlinked.</summary>
    public Guid GameId { get; }

    /// <summary>Gets the SharedGame ID that was previously linked.</summary>
    public Guid PreviousSharedGameId { get; }

    public GameUnlinkedFromSharedCatalogEvent(Guid gameId, Guid previousSharedGameId)
    {
        GameId = gameId;
        PreviousSharedGameId = previousSharedGameId;
    }
}
