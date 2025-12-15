using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

internal sealed class VectorDocumentIndexedEventHandler : DomainEventHandlerBase<VectorDocumentIndexedEvent>
{
    public VectorDocumentIndexedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<VectorDocumentIndexedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(VectorDocumentIndexedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(VectorDocumentIndexedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["Action"] = "VectorDocumentIndexed",
            ["DocumentId"] = domainEvent.DocumentId,
            ["GameId"] = domainEvent.GameId,
            ["ChunkCount"] = domainEvent.ChunkCount
        };
    }
}
