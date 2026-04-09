using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

internal sealed class VectorDocumentIndexedEvent : DomainEventBase
{
    public Guid DocumentId { get; }
    public Guid GameId { get; }
    public int ChunkCount { get; }

    /// <summary>
    /// Optional SharedGameId cross-reference (Issue #4921).
    /// Added as part of the library-to-game epic S2 tech-debt follow-up to
    /// eliminate the cross-BC read in VectorDocumentIndexedForKbFlagHandler.
    /// Null when the vector document has not been linked to a shared-catalog
    /// entry yet.
    /// </summary>
    public Guid? SharedGameId { get; }

    public VectorDocumentIndexedEvent(Guid documentId, Guid gameId, int chunkCount, Guid? sharedGameId = null)
    {
        DocumentId = documentId;
        GameId = gameId;
        ChunkCount = chunkCount;
        SharedGameId = sharedGameId;
    }
}
