using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

internal sealed class VectorDocumentIndexedEvent : DomainEventBase
{
    public Guid DocumentId { get; }
    public Guid? GameId { get; }
    /// <summary>Issue #4921: Set for admin-owned shared game content.</summary>
    public Guid? SharedGameId { get; }
    public int ChunkCount { get; }

    public VectorDocumentIndexedEvent(Guid documentId, Guid? gameId, Guid? sharedGameId, int chunkCount)
    {
        DocumentId = documentId;
        GameId = gameId;
        SharedGameId = sharedGameId;
        ChunkCount = chunkCount;
    }
}
