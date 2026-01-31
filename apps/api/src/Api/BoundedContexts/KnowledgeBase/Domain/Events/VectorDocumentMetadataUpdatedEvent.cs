using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

internal sealed class VectorDocumentMetadataUpdatedEvent : DomainEventBase
{
    public Guid DocumentId { get; }
    public string? NewMetadata { get; }

    public VectorDocumentMetadataUpdatedEvent(Guid documentId, string? newMetadata)
    {
        DocumentId = documentId;
        NewMetadata = newMetadata;
    }
}
