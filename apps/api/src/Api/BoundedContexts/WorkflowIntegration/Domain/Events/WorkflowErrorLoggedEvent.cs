using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.WorkflowIntegration.Domain.Events;

/// <summary>
/// Domain event raised when a workflow execution error is logged.
/// </summary>
public sealed class WorkflowErrorLoggedEvent : DomainEventBase
{
    public Guid ErrorLogId { get; }
    public string WorkflowId { get; }
    public string ExecutionId { get; }
    public string ErrorMessage { get; }
    public string? NodeName { get; }
    public string? StackTrace { get; }

    public WorkflowErrorLoggedEvent(
        Guid errorLogId,
        string workflowId,
        string executionId,
        string errorMessage,
        string? nodeName = null,
        string? stackTrace = null)
    {
        ErrorLogId = errorLogId;
        WorkflowId = workflowId;
        ExecutionId = executionId;
        ErrorMessage = errorMessage;
        NodeName = nodeName;
        StackTrace = stackTrace;
    }
}
