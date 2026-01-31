using Api.SharedKernel.Application.Interfaces;
using Api.Services;

namespace Api.BoundedContexts.Administration.Application.Queries.PromptEvaluation;

/// <summary>
/// Query to generate a formatted report for an evaluation result
/// Supports Markdown (human-readable) and JSON (machine-readable) formats
/// ADMIN-01 Phase 4: Prompt Testing Framework
/// </summary>
internal sealed record GenerateEvaluationReportQuery : IQuery<(string Report, string ContentType)>
{
    /// <summary>Evaluation ID to generate report for</summary>
    public required string EvaluationId { get; init; }

    /// <summary>Report format (Markdown or JSON)</summary>
    public ReportFormat Format { get; init; } = ReportFormat.Markdown;
}
