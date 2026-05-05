using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;

/// <summary>
/// Lightweight query that returns the current golden-set version hash for the specified
/// shared game. The frontend uses this to detect drift between the golden set and the
/// last-computed metrics snapshot, avoiding the cost of loading the full golden DTO.
/// </summary>
/// <remarks>
/// ADR-051 Sprint 1, Phase 6 / Task 28. Cached for 10 minutes via
/// <c>IHybridCacheService</c> under key
/// <c>meepleai:mechanic-golden:version-hash:{sharedGameId}</c> with tags
/// <c>game:{sharedGameId}</c> and <c>mechanic-golden</c> (matching the TTL/tags of
/// <see cref="GetGoldenForGameQuery"/> so cache invalidations stay consistent).
/// Returns <see cref="string.Empty"/> when no golden claims exist for the game.
/// </remarks>
internal sealed record GetGoldenVersionHashQuery(Guid SharedGameId) : IQuery<string>;
