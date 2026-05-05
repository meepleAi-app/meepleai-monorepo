using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;

/// <summary>
/// Parameterless query that returns the per-game summary feed for the admin
/// AI-comprehension validation dashboard. Each <see cref="DashboardGameRow"/>
/// carries the latest certification status and overall score for a shared game.
/// </summary>
/// <remarks>
/// ADR-051 Sprint 1, Phase 6 / Task 29. Cached for 5 minutes via
/// <c>IHybridCacheService</c> under key
/// <c>meepleai:mechanic-validation:dashboard</c> with tags
/// <c>mechanic-golden</c> (so golden-set updates invalidate the dashboard too)
/// and <c>mechanic-validation-dashboard</c> (for targeted invalidation from
/// metrics-computation and certification-override handlers). The TTL is tighter
/// than other validation queries because dashboard data turns over rapidly.
/// </remarks>
internal sealed record GetDashboardQuery() : IQuery<IReadOnlyList<DashboardGameRow>>;
