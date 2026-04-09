using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Api.Models;
using Api.Services;

namespace Api.DevTools.MockImpls;

/// <summary>
/// No-op mock of <see cref="IN8NTemplateService"/>.
/// All methods return safe defaults — no file I/O or n8n API calls are made.
/// Template imports are silently accepted and return a synthetic workflow ID.
/// </summary>
internal sealed class MockN8nTemplateService : IN8NTemplateService
{
    /// <inheritdoc />
    public Task<List<WorkflowTemplateDto>> GetTemplatesAsync(
        string? category = null,
        CancellationToken ct = default)
        => Task.FromResult(new List<WorkflowTemplateDto>());

    /// <inheritdoc />
    public Task<WorkflowTemplateDetailDto?> GetTemplateAsync(
        string templateId,
        CancellationToken ct = default)
        => Task.FromResult<WorkflowTemplateDetailDto?>(null);

    /// <inheritdoc />
    public Task<ImportTemplateResponse> ImportTemplateAsync(
        string templateId,
        IDictionary<string, string> parameters,
        string userId,
        CancellationToken ct = default)
        => Task.FromResult(new ImportTemplateResponse(
            WorkflowId: "MOCK-N8N-WORKFLOW",
            Message: "Mock import — no workflow created in n8n"));

    /// <inheritdoc />
    public ValidateTemplateResponse ValidateTemplate(string templateJson)
        => new ValidateTemplateResponse(IsValid: true, Errors: null);
}
