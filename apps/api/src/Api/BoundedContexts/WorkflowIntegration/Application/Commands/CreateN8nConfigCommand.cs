using Api.BoundedContexts.WorkflowIntegration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Commands;

public record CreateN8NConfigCommand(
    string Name,
    string BaseUrl,
    string ApiKeyEncrypted,
    Guid CreatedByUserId,
    string? WebhookUrl = null
) : ICommand<N8NConfigurationDto>;
