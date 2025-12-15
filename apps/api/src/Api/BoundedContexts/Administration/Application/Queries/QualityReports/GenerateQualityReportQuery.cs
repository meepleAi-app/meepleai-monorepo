using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.QualityReports;

/// <summary>
/// Query to generate a quality report for AI responses within a specified date range.
/// Aggregates metrics: RAG confidence, LLM confidence, citation quality, overall confidence, low-quality percentage.
/// </summary>
internal sealed record GenerateQualityReportQuery : IQuery<QualityReport>
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int Days { get; init; }
}
