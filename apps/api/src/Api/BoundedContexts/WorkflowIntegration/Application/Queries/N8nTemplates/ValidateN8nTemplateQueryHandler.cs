using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Queries.N8nTemplates;

/// <summary>
/// Handles n8n workflow template JSON validation.
/// Business logic: Template structure validation, required field checks, parameter validation.
/// Infrastructure delegation: JSON deserialization via N8nTemplateService.
/// </summary>
public sealed class ValidateN8nTemplateQueryHandler : IQueryHandler<ValidateN8nTemplateQuery, ValidateTemplateResponse>
{
    private readonly N8nTemplateService _templateService;
    private readonly ILogger<ValidateN8nTemplateQueryHandler> _logger;

    public ValidateN8nTemplateQueryHandler(
        N8nTemplateService templateService,
        ILogger<ValidateN8nTemplateQueryHandler> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    public Task<ValidateTemplateResponse> Handle(ValidateN8nTemplateQuery query, CancellationToken cancellationToken)
    {
        // Business logic validation
        if (string.IsNullOrWhiteSpace(query.TemplateJson))
        {
            _logger.LogWarning("Template JSON is required for validation");
            return Task.FromResult(new ValidateTemplateResponse(
                false,
                new List<string> { "Template JSON is required" }
            ));
        }

        _logger.LogInformation("Validating n8n template JSON (length: {Length} chars)", query.TemplateJson.Length);

        // Delegate to infrastructure service for validation logic
        var result = _templateService.ValidateTemplate(query.TemplateJson);

        _logger.LogInformation("Template validation result: {IsValid}, Errors: {ErrorCount}",
            result.IsValid, result.Errors?.Count ?? 0);

        return Task.FromResult(result);
    }
}
