using Api.BoundedContexts.WorkflowIntegration.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.WorkflowIntegration.Application.EventHandlers;

/// <summary>
/// Handler for WorkflowErrorLoggedEvent domain event.
/// </summary>
public sealed class WorkflowErrorLoggedEventHandler : DomainEventHandlerBase<WorkflowErrorLoggedEvent>
{
    public WorkflowErrorLoggedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<WorkflowErrorLoggedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(WorkflowErrorLoggedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Future: Send alert to operations team
        // Future: Trigger automatic error analysis
        // Future: Update error metrics dashboard
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(WorkflowErrorLoggedEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["ErrorLogId"] = domainEvent.ErrorLogId,
            ["WorkflowId"] = domainEvent.WorkflowId,
            ["ExecutionId"] = domainEvent.ExecutionId,
            ["ErrorMessage"] = domainEvent.ErrorMessage,
            ["NodeName"] = domainEvent.NodeName,
            ["Action"] = "WorkflowErrorLogged"
        };
    }
}
