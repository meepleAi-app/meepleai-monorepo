using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;

/// <summary>
/// Loads the curated golden-set (mechanic claims + BGG tags + version hash) for the specified
/// shared game, used by the admin AI Comprehension Validation UI.
/// </summary>
/// <remarks>
/// ADR-051 Sprint 1, Phase 6 / Task 27. Cached for 10 minutes via
/// <c>IHybridCacheService</c> under key <c>meepleai:mechanic-golden:{sharedGameId}</c>
/// with tags <c>game:{sharedGameId}</c> and <c>mechanic-golden</c>.
/// </remarks>
internal sealed record GetGoldenForGameQuery(Guid SharedGameId) : IQuery<GoldenForGameDto>;
