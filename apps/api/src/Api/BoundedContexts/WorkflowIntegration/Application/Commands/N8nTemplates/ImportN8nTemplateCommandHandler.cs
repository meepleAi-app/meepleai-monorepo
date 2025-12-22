using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Commands.N8NTemplates;

/// <summary>
/// Handles n8n workflow template import with parameter substitution.
/// Business logic: Template validation, parameter validation, workflow creation orchestration.
/// Infrastructure delegation: File I/O, n8n API calls, encryption via N8NTemplateService.
/// Flow: Load template → Validate parameters → Substitute placeholders → Query active config → Create in n8n.
/// </summary>
internal sealed class ImportN8NTemplateCommandHandler : ICommandHandler<ImportN8NTemplateCommand, ImportTemplateResponse>
{
    private readonly IN8NTemplateService _templateService;
    private readonly ILogger<ImportN8NTemplateCommandHandler> _logger;

    public ImportN8NTemplateCommandHandler(
        IN8NTemplateService templateService,
        ILogger<ImportN8NTemplateCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(templateService);
        _templateService = templateService;
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public async Task<ImportTemplateResponse> Handle(ImportN8NTemplateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Business logic validation
        if (string.IsNullOrWhiteSpace(command.TemplateId))
        {
            throw new ArgumentException("Template ID is required", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.UserId))
        {
            throw new ArgumentException("User ID is required", nameof(command));
        }

        _logger.LogInformation(
            "User {UserId} importing n8n template {TemplateId} with {ParameterCount} parameters",
            command.UserId, command.TemplateId, command.Parameters.Count);

        // Delegate to infrastructure service for complex orchestration:
        // - Template loading from file system
        // - Parameter validation and substitution
        // - Database query for active n8n config
        // - n8n API call to create workflow
        // - API key decryption
        var result = await _templateService.ImportTemplateAsync(
            command.TemplateId,
            command.Parameters,
            command.UserId,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Template {TemplateId} imported successfully as workflow {WorkflowId} for user {UserId}",
            command.TemplateId, result.WorkflowId, command.UserId);

        return result;
    }
}
