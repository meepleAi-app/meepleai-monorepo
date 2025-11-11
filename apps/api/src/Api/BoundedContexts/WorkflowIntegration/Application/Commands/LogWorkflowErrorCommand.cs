using Api.BoundedContexts.WorkflowIntegration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Commands;

public record LogWorkflowErrorCommand(
    string WorkflowId,
    string ExecutionId,
    string ErrorMessage,
    string? NodeName = null,
    string? StackTrace = null
) : ICommand<WorkflowErrorLogDto>;
