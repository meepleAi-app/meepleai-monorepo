using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Queries.N8nTemplates;

/// <summary>
/// Handles retrieval of all n8n workflow templates with optional category filtering.
/// Business logic: Category filtering, sorting by category and name.
/// Infrastructure delegation: File I/O and JSON deserialization via N8nTemplateService.
/// </summary>
public sealed class GetN8nTemplatesQueryHandler : IQueryHandler<GetN8nTemplatesQuery, List<WorkflowTemplateDto>>
{
    private readonly N8nTemplateService _templateService;
    private readonly ILogger<GetN8nTemplatesQueryHandler> _logger;

    public GetN8nTemplatesQueryHandler(
        N8nTemplateService templateService,
        ILogger<GetN8nTemplatesQueryHandler> logger)
    {
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<WorkflowTemplateDto>> Handle(GetN8nTemplatesQuery query, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving n8n templates{CategoryFilter}",
            query.Category != null ? $" for category '{query.Category}'" : string.Empty);

        // Delegate to infrastructure service for file I/O and deserialization
        var templates = await _templateService.GetTemplatesAsync(query.Category, cancellationToken);

        _logger.LogInformation("Retrieved {Count} n8n templates", templates.Count);

        return templates;
    }
}
