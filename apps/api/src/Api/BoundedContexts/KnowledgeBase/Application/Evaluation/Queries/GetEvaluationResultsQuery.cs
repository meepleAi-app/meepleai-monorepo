using MediatR;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Queries;

/// <summary>
/// Query to retrieve past evaluation results.
/// </summary>
public sealed record GetEvaluationResultsQuery : IRequest<IReadOnlyList<EvaluationResult>>
{
    /// <summary>
    /// Filter by dataset name.
    /// </summary>
    public string? DatasetName { get; init; }

    /// <summary>
    /// Filter by configuration.
    /// </summary>
    public string? Configuration { get; init; }

    /// <summary>
    /// Maximum number of results to return.
    /// </summary>
    public int Limit { get; init; } = 10;
}

/// <summary>
/// Query to get the latest baseline metrics.
/// </summary>
public sealed record GetBaselineMetricsQuery : IRequest<EvaluationMetrics?>
{
    /// <summary>
    /// Dataset name to get baseline for.
    /// </summary>
    public string? DatasetName { get; init; }
}
