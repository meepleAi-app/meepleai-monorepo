using Api.BoundedContexts.WorkflowIntegration.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.WorkflowIntegration.Application.EventHandlers;

/// <summary>
/// Handler for WorkflowRetriedEvent domain event.
/// </summary>
public sealed class WorkflowRetriedEventHandler : DomainEventHandlerBase<WorkflowRetriedEvent>
{
    public WorkflowRetriedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<WorkflowRetriedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(WorkflowRetriedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Future: Track retry patterns for workflow optimization
        // Future: Alert if retry count approaches max retries
        await Task.CompletedTask;
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(WorkflowRetriedEvent domainEvent)
    {
        return new Dictionary<string, object?>
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
