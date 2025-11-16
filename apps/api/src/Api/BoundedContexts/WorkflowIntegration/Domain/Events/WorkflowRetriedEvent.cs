using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.WorkflowIntegration.Domain.Events;

/// <summary>
/// Domain event raised when a workflow error is retried.
/// </summary>
public sealed class WorkflowRetriedEvent : DomainEventBase
{
    public Guid ErrorLogId { get; }
    public string WorkflowId { get; }
    public string ExecutionId { get; }
    public int RetryCount { get; }
    public int MaxRetries { get; }

    public WorkflowRetriedEvent(
        Guid errorLogId,
        string workflowId,
        string executionId,
        int retryCount,
        int maxRetries = 3)
    {
        ErrorLogId = errorLogId;
        WorkflowId = workflowId;
        ExecutionId = executionId;
        RetryCount = retryCount;
        MaxRetries = maxRetries;
    }
}
