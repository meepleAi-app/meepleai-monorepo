using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Queries.N8NTemplates;

/// <summary>
/// Query to get all available n8n workflow templates, optionally filtered by category.
/// Returns template metadata without full workflow details.
/// </summary>
public sealed record GetN8NTemplatesQuery : IQuery<List<WorkflowTemplateDto>>
{
    public string? Category { get; init; }
}
