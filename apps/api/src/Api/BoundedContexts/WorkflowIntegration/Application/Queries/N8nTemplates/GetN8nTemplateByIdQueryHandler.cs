using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Queries.N8NTemplates;

/// <summary>
/// Handles retrieval of a specific n8n workflow template by ID with full details.
/// Business logic: Template existence validation, path security.
/// Infrastructure delegation: File I/O and JSON deserialization via N8NTemplateService.
/// Security: Path traversal prevention handled by infrastructure service.
/// </summary>
internal sealed class GetN8NTemplateByIdQueryHandler : IQueryHandler<GetN8NTemplateByIdQuery, WorkflowTemplateDetailDto?>
{
    private readonly N8NTemplateService _templateService;
    private readonly ILogger<GetN8NTemplateByIdQueryHandler> _logger;

    public GetN8NTemplateByIdQueryHandler(
        N8NTemplateService templateService,
        ILogger<GetN8NTemplateByIdQueryHandler> logger)
    {
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WorkflowTemplateDetailDto?> Handle(GetN8NTemplateByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        // Business logic validation
        if (string.IsNullOrWhiteSpace(query.TemplateId))
        {
            _logger.LogWarning("Template ID is required");
            return null;
        }

        _logger.LogInformation("Retrieving n8n template {TemplateId}", query.TemplateId);

        // Delegate to infrastructure service (includes path security validation)
        var template = await _templateService.GetTemplateAsync(query.TemplateId, cancellationToken).ConfigureAwait(false);

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
