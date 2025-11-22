using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Queries.N8nTemplates;

/// <summary>
/// Query to get all available n8n workflow templates, optionally filtered by category.
/// Returns template metadata without full workflow details.
/// </summary>
public sealed record GetN8nTemplatesQuery : IQuery<List<WorkflowTemplateDto>>
{
    public string? Category { get; init; }
}
