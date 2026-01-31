using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Queries.N8NTemplates;

/// <summary>
/// Handles retrieval of all n8n workflow templates with optional category filtering.
/// Business logic: Category filtering, sorting by category and name.
/// Infrastructure delegation: File I/O and JSON deserialization via N8NTemplateService.
/// </summary>
internal sealed class GetN8NTemplatesQueryHandler : IQueryHandler<GetN8NTemplatesQuery, List<WorkflowTemplateDto>>
{
    private readonly IN8NTemplateService _templateService;
    private readonly ILogger<GetN8NTemplatesQueryHandler> _logger;

    public GetN8NTemplatesQueryHandler(
        IN8NTemplateService templateService,
        ILogger<GetN8NTemplatesQueryHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(templateService);
        _templateService = templateService;
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public async Task<List<WorkflowTemplateDto>> Handle(GetN8NTemplatesQuery query, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving n8n templates{CategoryFilter}",
            query.Category != null ? $" for category '{query.Category}'" : string.Empty);

        // Delegate to infrastructure service for file I/O and deserialization
        var templates = await _templateService.GetTemplatesAsync(query.Category, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Retrieved {Count} n8n templates", templates.Count);

        return templates;
    }
}
