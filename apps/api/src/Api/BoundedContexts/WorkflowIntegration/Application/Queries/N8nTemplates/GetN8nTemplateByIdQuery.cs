using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Queries.N8NTemplates;

/// <summary>
/// Query to get a specific n8n workflow template by ID with full details including workflow JSON.
/// Returns null if template not found.
/// </summary>
internal sealed record GetN8NTemplateByIdQuery : IQuery<WorkflowTemplateDetailDto?>
{
    public string TemplateId { get; init; } = string.Empty;
}
