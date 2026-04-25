namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Persistence entity for <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicAnalysisMetrics"/>.
/// Plain POCO — all invariants are enforced by the domain aggregate; this type exists only to be shaped by EF Core.
/// Rows are insert-only; no optimistic concurrency token required.
/// </summary>
public class MechanicAnalysisMetricsEntity
{
    public Guid Id { get; set; }
    public Guid MechanicAnalysisId { get; set; }
    public Guid SharedGameId { get; set; }

    public decimal CoveragePct { get; set; }
    public decimal PageAccuracyPct { get; set; }
    public decimal BggMatchPct { get; set; }
    public decimal OverallScore { get; set; }

    /// <summary>0=NotEvaluated, 1=Certified, 2=NotCertified.
    /// Mirrors <c>Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects.CertificationStatus</c>.</summary>
    public int CertificationStatus { get; set; }

    public string GoldenVersionHash { get; set; } = string.Empty;

    /// <summary>Serialised <c>CertificationThresholds</c> snapshot at computation time (jsonb column).</summary>
    public string ThresholdsSnapshotJson { get; set; } = "{}";

    /// <summary>Per-claim match details (jsonb column).</summary>
    public string MatchDetailsJson { get; set; } = "[]";

    public DateTimeOffset ComputedAt { get; set; }

    // === Navigation ===
    public MechanicAnalysisEntity MechanicAnalysis { get; set; } = default!;
    public SharedGameEntity SharedGame { get; set; } = default!;
}
