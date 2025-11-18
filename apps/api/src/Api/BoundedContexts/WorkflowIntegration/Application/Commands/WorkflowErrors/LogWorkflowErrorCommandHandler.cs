using System;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Commands.WorkflowErrors;

/// <summary>
/// Handles workflow error logging from n8n webhooks.
/// Business logic: Error message sanitization (removes API keys, tokens, passwords), truncation.
/// Infrastructure delegation: Database persistence, cache invalidation via WorkflowErrorLoggingService.
/// Resilience: Never fails n8n webhook operations (catch-all for telemetry reliability).
/// </summary>
public sealed class LogWorkflowErrorCommandHandler : ICommandHandler<LogWorkflowErrorCommand>
{
    private readonly IWorkflowErrorLoggingService _errorLoggingService;
    private readonly ILogger<LogWorkflowErrorCommandHandler> _logger;

    public LogWorkflowErrorCommandHandler(
        IWorkflowErrorLoggingService errorLoggingService,
        ILogger<LogWorkflowErrorCommandHandler> logger)
    {
        _errorLoggingService = errorLoggingService;
        _logger = logger;
    }

    public async Task Handle(LogWorkflowErrorCommand command, CancellationToken cancellationToken)
    {
        var executionId = command.ExecutionId;
        if (string.IsNullOrWhiteSpace(executionId))
        {
            executionId = $"missing-exec-{Guid.NewGuid():N}";
            _logger.LogWarning(
                "ExecutionId was not provided for workflow {WorkflowId}; using generated placeholder {ExecutionId}",
                command.WorkflowId,
                executionId);
        }

        _logger.LogInformation(
            "Logging workflow error: WorkflowId={WorkflowId}, ExecutionId={ExecutionId}, NodeName={NodeName}",
            command.WorkflowId, executionId, command.NodeName);

        // Create request DTO
        var request = new Api.Models.LogWorkflowErrorRequest(
            WorkflowId: command.WorkflowId,
            ExecutionId: executionId,
            ErrorMessage: command.ErrorMessage,
            NodeName: command.NodeName,
            RetryCount: command.RetryCount,
            StackTrace: command.StackTrace
        );

        // Delegate to infrastructure service for:
        // - Error message sanitization (removes sensitive data: API keys, tokens, passwords)
        // - Database persistence
        // - Cache invalidation
        // - Resilience pattern (never fails webhook)
        await _errorLoggingService.LogErrorAsync(request, cancellationToken);

        _logger.LogInformation("Workflow error logged successfully for WorkflowId={WorkflowId}", command.WorkflowId);
    }
}
