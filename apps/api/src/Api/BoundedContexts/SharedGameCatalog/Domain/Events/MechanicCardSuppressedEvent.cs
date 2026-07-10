using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Raised when a <see cref="Aggregates.MechanicCard"/> is suppressed (takedown, #529). Consumed by
/// downstream cache-invalidation handlers so the player-facing card view (#528) drops the card.
/// </summary>
internal sealed class MechanicCardSuppressedEvent : DomainEventBase
{
    public Guid CardId { get; }

    public Guid SharedGameId { get; }

    public Guid ActorId { get; }

    public string Reason { get; }

    public MechanicCardSuppressedEvent(Guid cardId, Guid sharedGameId, Guid actorId, string reason)
    {
        CardId = cardId;
        SharedGameId = sharedGameId;
        ActorId = actorId;
        Reason = reason;
    }
}
