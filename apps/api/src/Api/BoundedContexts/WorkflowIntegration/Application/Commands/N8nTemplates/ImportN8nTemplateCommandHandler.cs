using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Commands.N8nTemplates;

/// <summary>
/// Handles n8n workflow template import with parameter substitution.
/// Business logic: Template validation, parameter validation, workflow creation orchestration.
/// Infrastructure delegation: File I/O, n8n API calls, encryption via N8nTemplateService.
/// Flow: Load template → Validate parameters → Substitute placeholders → Query active config → Create in n8n.
/// </summary>
public sealed class ImportN8nTemplateCommandHandler : ICommandHandler<ImportN8nTemplateCommand, ImportTemplateResponse>
{
    private readonly N8nTemplateService _templateService;
    private readonly ILogger<ImportN8nTemplateCommandHandler> _logger;

    public ImportN8nTemplateCommandHandler(
        N8nTemplateService templateService,
        ILogger<ImportN8nTemplateCommandHandler> logger)
    {
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ImportTemplateResponse> Handle(ImportN8nTemplateCommand command, CancellationToken cancellationToken)
    {
        // Business logic validation
        if (string.IsNullOrWhiteSpace(command.TemplateId))
        {
            throw new ArgumentException("Template ID is required", nameof(command.TemplateId));
        }

        if (string.IsNullOrWhiteSpace(command.UserId))
        {
            throw new ArgumentException("User ID is required", nameof(command.UserId));
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
            cancellationToken);

        _logger.LogInformation(
            "Template {TemplateId} imported successfully as workflow {WorkflowId} for user {UserId}",
            command.TemplateId, result.WorkflowId, command.UserId);

        return result;
    }
}
