using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to retrieve cache statistics with optional game filter.
/// Admin-only operation for monitoring cache performance.
/// </summary>
/// <param name="GameId">Optional game ID filter (null = all games)</param>
internal record GetCacheStatsQuery(
    string? GameId = null
) : IQuery<CacheStats>;
