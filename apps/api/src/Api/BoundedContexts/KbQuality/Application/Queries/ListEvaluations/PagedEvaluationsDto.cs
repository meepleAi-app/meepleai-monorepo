namespace Api.BoundedContexts.KbQuality.Application.Queries.ListEvaluations;

/// <summary>
/// Standard paginated wrapper for the eval history list view (#1675).
/// </summary>
public sealed record PagedEvaluationsDto(
    IReadOnlyList<EvaluationRunListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// Slim projection for the eval history list: enough to render a row + sparkline + status chip
/// without loading the full <see cref="GetEvaluation.EvaluationMetricsDto"/> graph. All metric
/// fields are nullable because non-terminal runs (Pending/GoldsetGenerating/Running/Failed)
/// have no metrics attached.
/// </summary>
public sealed record EvaluationRunListItemDto(
    Guid EvaluationId,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string Status,
    string GoldsetVersion,
    double? PrecisionAt5,
    double? Mrr,
    int? LatencyP95Ms,
    decimal? CostUsd,
    string? QualityBand);
