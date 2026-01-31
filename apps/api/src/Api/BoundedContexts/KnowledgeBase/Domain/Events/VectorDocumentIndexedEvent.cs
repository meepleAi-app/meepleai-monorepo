using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

internal sealed class VectorDocumentIndexedEvent : DomainEventBase
{
    public Guid DocumentId { get; }
    public Guid GameId { get; }
    public int ChunkCount { get; }

    public VectorDocumentIndexedEvent(Guid documentId, Guid gameId, int chunkCount)
    {
        DocumentId = documentId;
        GameId = gameId;
        ChunkCount = chunkCount;
    }
}
