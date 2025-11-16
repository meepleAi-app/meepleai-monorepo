using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

public sealed class VectorDocumentSearchedEvent : DomainEventBase
{
    public Guid DocumentId { get; }
    public string Query { get; }

    public VectorDocumentSearchedEvent(Guid documentId, string query)
    {
        DocumentId = documentId;
        Query = query;
    }
}
