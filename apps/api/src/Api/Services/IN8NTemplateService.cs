using Api.Models;

namespace Api.Services;

/// <summary>
/// Service interface for n8n workflow template operations.
/// Provides template discovery, validation, and import capabilities.
/// </summary>
public interface IN8NTemplateService
{
    /// <summary>
    /// Get all available templates, optionally filtered by category.
    /// </summary>
    Task<List<WorkflowTemplateDto>> GetTemplatesAsync(
        string? category = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get a specific template by ID with full workflow details.
    /// </summary>
    Task<WorkflowTemplateDetailDto?> GetTemplateAsync(
        string templateId,
        CancellationToken ct = default);

    /// <summary>
    /// Import a template into n8n by creating a new workflow with substituted parameters.
    /// </summary>
    Task<ImportTemplateResponse> ImportTemplateAsync(
        string templateId,
        IDictionary<string, string> parameters,
        string userId,
        CancellationToken ct = default);

    /// <summary>
    /// Validate template JSON structure.
    /// </summary>
    ValidateTemplateResponse ValidateTemplate(string templateJson);
}
