using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Queries.N8nTemplates;

/// <summary>
/// Handles retrieval of a specific n8n workflow template by ID with full details.
/// Business logic: Template existence validation, path security.
/// Infrastructure delegation: File I/O and JSON deserialization via N8nTemplateService.
/// Security: Path traversal prevention handled by infrastructure service.
/// </summary>
public sealed class GetN8nTemplateByIdQueryHandler : IQueryHandler<GetN8nTemplateByIdQuery, WorkflowTemplateDetailDto?>
{
    private readonly N8nTemplateService _templateService;
    private readonly ILogger<GetN8nTemplateByIdQueryHandler> _logger;

    public GetN8nTemplateByIdQueryHandler(
        N8nTemplateService templateService,
        ILogger<GetN8nTemplateByIdQueryHandler> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    public async Task<WorkflowTemplateDetailDto?> Handle(GetN8nTemplateByIdQuery query, CancellationToken cancellationToken)
    {
        // Business logic validation
        if (string.IsNullOrWhiteSpace(query.TemplateId))
        {
            _logger.LogWarning("Template ID is required");
            return null;
        }

        _logger.LogInformation("Retrieving n8n template {TemplateId}", query.TemplateId);

        // Delegate to infrastructure service (includes path security validation)
        var template = await _templateService.GetTemplateAsync(query.TemplateId, cancellationToken);

        if (template == null)
        {
            _logger.LogWarning("Template {TemplateId} not found", query.TemplateId);
        }
        else
        {
            _logger.LogInformation("Retrieved template {TemplateId}: {TemplateName}", query.TemplateId, template.Name);
        }

        return template;
    }
}
