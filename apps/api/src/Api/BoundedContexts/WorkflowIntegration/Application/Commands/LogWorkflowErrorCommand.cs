using Api.BoundedContexts.WorkflowIntegration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Commands;

internal record LogWorkflowErrorCommand(
    string WorkflowId,
    string ExecutionId,
    string ErrorMessage,
    string? NodeName = null,
    string? StackTrace = null
) : ICommand<WorkflowErrorLogDto>;
