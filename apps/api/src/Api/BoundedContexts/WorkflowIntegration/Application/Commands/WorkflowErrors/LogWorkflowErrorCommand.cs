using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Commands.WorkflowErrors;

/// <summary>
/// Command to log a workflow error from n8n webhook.
/// Sanitizes error messages to remove sensitive data and stores in database.
/// Used by n8n error handling webhooks for telemetry and debugging.
/// </summary>
public sealed record LogWorkflowErrorCommand : ICommand
{
    public string WorkflowId { get; init; } = string.Empty;
    public string? ExecutionId { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public string? NodeName { get; init; }
    public int RetryCount { get; init; }
    public string? StackTrace { get; init; }
}
