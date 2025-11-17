using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Commands.N8nTemplates;

/// <summary>
/// Command to import an n8n workflow template by creating a new workflow with parameter substitution.
/// Validates parameters, substitutes placeholders, and creates workflow in n8n via REST API.
/// </summary>
public sealed record ImportN8nTemplateCommand : ICommand<ImportTemplateResponse>
{
    public string TemplateId { get; init; } = string.Empty;
    public Dictionary<string, string> Parameters { get; init; } = new();
    public string UserId { get; init; } = string.Empty;
}
