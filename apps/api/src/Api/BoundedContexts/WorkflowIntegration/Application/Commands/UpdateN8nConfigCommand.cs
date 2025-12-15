using Api.BoundedContexts.WorkflowIntegration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Commands;

/// <summary>
/// Command to update an existing n8n configuration.
/// </summary>
internal record UpdateN8NConfigCommand(
    Guid ConfigId,
    string? Name,
    string? BaseUrl,
    string? WebhookUrl,
    string? ApiKeyEncrypted,
    bool? IsActive
) : ICommand<N8NConfigurationDto>;
