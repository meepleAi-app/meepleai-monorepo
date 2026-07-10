using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Raised when a <see cref="Aggregates.MechanicCard"/> is published from an approved analysis (#527).
/// Downstream consumers (e.g. RAG cache invalidation for the game, #528 player view cache) can
/// subscribe. The publish audit-log row is written by the command handler in the same transaction,
/// so this event is purely for cross-context reactions.
/// </summary>
internal sealed class MechanicCardPublishedEvent : DomainEventBase
{
    public Guid CardId { get; }

    public Guid SharedGameId { get; }

    public Guid OriginAnalysisId { get; }

    public Guid PublisherId { get; }

    public int Version { get; }

    public MechanicCardPublishedEvent(
        Guid cardId,
        Guid sharedGameId,
        Guid originAnalysisId,
        Guid publisherId,
        int version)
    {
        CardId = cardId;
        SharedGameId = sharedGameId;
        OriginAnalysisId = originAnalysisId;
        PublisherId = publisherId;
        Version = version;
    }
}
