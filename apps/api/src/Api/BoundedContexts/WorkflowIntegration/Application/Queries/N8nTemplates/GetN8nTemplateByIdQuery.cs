using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Queries.N8nTemplates;

/// <summary>
/// Query to get a specific n8n workflow template by ID with full details including workflow JSON.
/// Returns null if template not found.
/// </summary>
public sealed record GetN8nTemplateByIdQuery : IQuery<WorkflowTemplateDetailDto?>
{
    public string TemplateId { get; init; } = string.Empty;
}
