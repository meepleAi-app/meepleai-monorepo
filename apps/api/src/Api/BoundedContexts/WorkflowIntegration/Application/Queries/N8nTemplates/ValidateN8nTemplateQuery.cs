using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Queries.N8NTemplates;

/// <summary>
/// Query to validate n8n workflow template JSON structure.
/// Validates required fields, workflow structure, and parameter definitions.
/// </summary>
internal sealed record ValidateN8NTemplateQuery : IQuery<ValidateTemplateResponse>
{
    public string TemplateJson { get; init; } = string.Empty;
}
