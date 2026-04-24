using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;

/// <summary>
/// Returns up to <paramref name="Take"/> historical <see cref="MechanicAnalysisMetrics"/>
/// snapshots for the specified shared game, ordered by computation time descending.
/// Used to render the trend chart on the admin AI-comprehension validation detail page.
/// </summary>
/// <remarks>
/// ADR-051 Sprint 1, Phase 6 / Task 30. Cached for 5 minutes via
/// <c>IHybridCacheService</c> under key
/// <c>meepleai:mechanic-validation:trend:{SharedGameId}:{Take}</c>. Tagged with
/// <c>game:{SharedGameId}</c> for targeted invalidation when new metrics are
/// appended for that game, plus the broad <c>mechanic-validation-trend</c> tag
/// for nuclear invalidation if trend shape changes. <paramref name="Take"/>
/// defaults to 20 and is capped at 100 by
/// <see cref="GetTrendQueryValidator"/>.
/// </remarks>
internal sealed record GetTrendQuery(Guid SharedGameId, int Take = 20)
    : IQuery<IReadOnlyList<MechanicAnalysisMetrics>>;
