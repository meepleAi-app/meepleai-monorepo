using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Projection row returned by <see cref="IMechanicAnalysisMetricsRepository.GetDashboardAsync"/>.
/// Carries the per-game summary required by the admin validation dashboard.
/// </summary>
public sealed record DashboardGameRow(
    Guid SharedGameId,
    string Name,
    CertificationStatus Status,
    decimal OverallScore,
    DateTimeOffset? LastComputedAt);

/// <summary>
/// Repository interface for the <see cref="MechanicAnalysisMetrics"/> aggregate (ADR-051 Sprint 1).
/// Provides time-series storage and dashboard projections for AI comprehension validation scores.
/// </summary>
public interface IMechanicAnalysisMetricsRepository
{
    /// <summary>
    /// Stages a new <see cref="MechanicAnalysisMetrics"/> snapshot for insertion.
    /// Persistence occurs at <c>SaveChangesAsync</c>.
    /// </summary>
    Task AddAsync(MechanicAnalysisMetrics metrics, CancellationToken ct);

    /// <summary>
    /// Returns the metrics snapshot for the specified analysis run, or <c>null</c> if not found.
    /// </summary>
    Task<MechanicAnalysisMetrics?> GetByAnalysisAsync(Guid analysisId, CancellationToken ct);

    /// <summary>
    /// Returns the most recent metrics snapshot for the specified analysis run,
    /// or <c>null</c> if none exists.
    /// </summary>
    Task<MechanicAnalysisMetrics?> GetLatestByAnalysisAsync(Guid analysisId, CancellationToken ct);

    /// <summary>
    /// Returns the per-game summary rows required by the admin validation dashboard.
    /// Each row reflects the latest computed score and certification status per shared game.
    /// </summary>
    Task<IReadOnlyList<DashboardGameRow>> GetDashboardAsync(CancellationToken ct);

    /// <summary>
    /// Returns up to <paramref name="take"/> historical metric snapshots for the specified shared game,
    /// ordered by computation time descending. Used for trend chart rendering on the admin detail page.
    /// </summary>
    Task<IReadOnlyList<MechanicAnalysisMetrics>> GetTrendAsync(Guid sharedGameId, int take, CancellationToken ct);
}
