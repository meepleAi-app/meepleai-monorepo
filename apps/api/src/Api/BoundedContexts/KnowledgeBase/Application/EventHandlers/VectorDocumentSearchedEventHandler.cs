using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

public sealed class VectorDocumentSearchedEventHandler : DomainEventHandlerBase<VectorDocumentSearchedEvent>
{
    public VectorDocumentSearchedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<VectorDocumentSearchedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(VectorDocumentSearchedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        await Task.CompletedTask;
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(VectorDocumentSearchedEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["Action"] = "VectorDocumentSearched",
            ["DocumentId"] = domainEvent.DocumentId,
            ["Query"] = domainEvent.Query
        };
    }
}
