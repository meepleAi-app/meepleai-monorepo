using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Domain.Services;

/// <summary>
/// Domain service for generating reports based on templates
/// ISSUE-916: Core report generation logic
/// </summary>
internal interface IReportGeneratorService
{
    /// <summary>
    /// Generates a report based on template and parameters
    /// </summary>
    /// <param name="template">Report template type</param>
    /// <param name="format">Output format</param>
    /// <param name="parameters">Template-specific parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated report data</returns>
    Task<ReportData> GenerateAsync(
        ReportTemplate template,
        ReportFormat format,
        IReadOnlyDictionary<string, object> parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates parameters for a specific template
    /// </summary>
    /// <param name="template">Report template type</param>
    /// <param name="parameters">Parameters to validate</param>
    /// <returns>Validation result</returns>
    (bool IsValid, string? ErrorMessage) ValidateParameters(
        ReportTemplate template,
        IReadOnlyDictionary<string, object> parameters);
}

/// <summary>
/// Report data with content and metadata
/// </summary>
internal sealed record ReportData(
    byte[] Content,
    string FileName,
    long FileSizeBytes,
    IReadOnlyDictionary<string, object> Metadata);

