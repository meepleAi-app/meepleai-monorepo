using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.WorkflowIntegration.Domain.Entities;

/// <summary>
/// WorkflowErrorLog aggregate root representing an error from workflow execution.
/// </summary>
public sealed class WorkflowErrorLog : AggregateRoot<Guid>
{
    public string WorkflowId { get; private set; }
    public string ExecutionId { get; private set; }
    public string ErrorMessage { get; private set; }
    public string? NodeName { get; private set; }
    public int RetryCount { get; private set; }
    public string? StackTrace { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private WorkflowErrorLog() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new workflow error log entry.
    /// </summary>
    public WorkflowErrorLog(
        Guid id,
        string workflowId,
        string executionId,
        string errorMessage,
        string? nodeName = null,
        string? stackTrace = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
            throw new ArgumentException("WorkflowId cannot be empty", nameof(workflowId));

        if (string.IsNullOrWhiteSpace(executionId))
            throw new ArgumentException("ExecutionId cannot be empty", nameof(executionId));

        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("ErrorMessage cannot be empty", nameof(errorMessage));

        WorkflowId = workflowId.Trim();
        ExecutionId = executionId.Trim();
        ErrorMessage = errorMessage.Trim();
        NodeName = nodeName?.Trim();
        StackTrace = stackTrace;
        RetryCount = 0;
        CreatedAt = DateTime.UtcNow;

        // TODO: Add domain event WorkflowErrorLogged
    }

    /// <summary>
    /// Increments retry count.
    /// </summary>
    public void IncrementRetryCount()
    {
        RetryCount++;
        // TODO: Add domain event WorkflowRetried
    }

    /// <summary>
    /// Checks if error should be retried.
    /// </summary>
    public bool ShouldRetry(int maxRetries = 3) => RetryCount < maxRetries;
}
