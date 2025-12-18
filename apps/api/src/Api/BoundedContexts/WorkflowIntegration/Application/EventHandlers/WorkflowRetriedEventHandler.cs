using Api.BoundedContexts.WorkflowIntegration.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.WorkflowIntegration.Application.EventHandlers;

/// <summary>
/// Handler for WorkflowRetriedEvent domain event.
/// </summary>
internal sealed class WorkflowRetriedEventHandler : DomainEventHandlerBase<WorkflowRetriedEvent>
{
    public WorkflowRetriedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<WorkflowRetriedEventHandler> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(WorkflowRetriedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Future: Track retry patterns for workflow optimization
        // Future: Alert if retry count approaches max retries
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(WorkflowRetriedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["ErrorLogId"] = domainEvent.ErrorLogId,
            ["WorkflowId"] = domainEvent.WorkflowId,
            ["ExecutionId"] = domainEvent.ExecutionId,
            ["RetryCount"] = domainEvent.RetryCount,
            ["MaxRetries"] = domainEvent.MaxRetries,
            ["Action"] = "WorkflowRetried"
        };
    }
}
