using Api.Models;
using Api.SharedKernel.Application.Abstractions;
using ErrorOr;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.ValidationAccuracy;

/// <summary>
/// Query to retrieve validation accuracy baselines with optional filtering.
/// BGAI-039: Accuracy baseline measurement tracking.
/// </summary>
public record GetValidationAccuracyBaselinesQuery : IQuery<ErrorOr<List<ValidationAccuracyBaselineDto>>>
{
    /// <summary>
    /// Filter by context (optional)
    /// </summary>
    public string? Context { get; init; }

    /// <summary>
    /// Filter by dataset ID (optional)
    /// </summary>
    public string? DatasetId { get; init; }

    /// <summary>
    /// Filter to only baselines that meet threshold (optional)
    /// </summary>
    public bool? MeetsBaselineOnly { get; init; }

    /// <summary>
    /// Maximum number of results to return
    /// </summary>
    public int Limit { get; init; } = 10;
}
