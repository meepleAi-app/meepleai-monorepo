using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Queries.N8nTemplates;

/// <summary>
/// Query to validate n8n workflow template JSON structure.
/// Validates required fields, workflow structure, and parameter definitions.
/// </summary>
public sealed record ValidateN8nTemplateQuery : IQuery<ValidateTemplateResponse>
{
    public string TemplateJson { get; init; } = string.Empty;
}
