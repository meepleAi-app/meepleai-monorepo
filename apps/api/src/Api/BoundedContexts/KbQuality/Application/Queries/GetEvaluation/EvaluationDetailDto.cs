namespace Api.BoundedContexts.KbQuality.Application.Queries.GetEvaluation;

/// <summary>
/// Full projection of a <see cref="Domain.Evaluation.DocumentEvaluationRun"/> for the
/// admin UI detail panel (#1675 Task 15). <see cref="Metrics"/> is <c>null</c> for runs
/// not yet in a terminal Completed state. Latency is flattened to integer milliseconds
/// for direct rendering without TimeSpan-to-string formatting on the FE.
/// </summary>
public sealed record EvaluationDetailDto(
    Guid EvaluationId,
    Guid PdfDocumentId,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string Status,
    string GoldsetVersion,
    long GoldsetGenerationSeed,
    EvaluationMetricsDto? Metrics,
    decimal? CostUsd,
    Guid TriggeredByAdminId,
    string? ErrorMessage);

public sealed record EvaluationMetricsDto(
    PrecisionDto Precision,
    RankingDto Ranking,
    LatencyDto Latency,
    int QueryCount,
    decimal CostUsd,
    string QualityBand);

public sealed record PrecisionDto(double At1, double At3, double At5);

public sealed record RankingDto(double Mrr);

public sealed record LatencyDto(int P50Ms, int P95Ms);
