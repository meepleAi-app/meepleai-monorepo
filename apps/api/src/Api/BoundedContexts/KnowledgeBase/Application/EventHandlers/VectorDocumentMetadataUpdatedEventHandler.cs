using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

internal sealed class VectorDocumentMetadataUpdatedEventHandler : DomainEventHandlerBase<VectorDocumentMetadataUpdatedEvent>
{
    public VectorDocumentMetadataUpdatedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<VectorDocumentMetadataUpdatedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(VectorDocumentMetadataUpdatedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(VectorDocumentMetadataUpdatedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["Action"] = "VectorDocumentMetadataUpdated",
            ["DocumentId"] = domainEvent.DocumentId,
            ["NewMetadata"] = domainEvent.NewMetadata
        };
    }
}
