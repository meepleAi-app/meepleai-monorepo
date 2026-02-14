using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handler for GetTrendingGamesQuery with Redis caching.
/// Issue #4310: Catalog Trending Analytics.
/// </summary>
internal class GetTrendingGamesQueryHandler : IQueryHandler<GetTrendingGamesQuery, TrendingGamesResponseDto>
{
    private readonly HybridCache _cache;
    private readonly ILogger<GetTrendingGamesQueryHandler> _logger;

    public GetTrendingGamesQueryHandler(
        HybridCache cache,
        ILogger<GetTrendingGamesQueryHandler> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<TrendingGamesResponseDto> Handle(
        GetTrendingGamesQuery query,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"trending:games:{query.Period}";

        try
        {
            var cached = await _cache.GetOrCreateAsync(
                cacheKey,
                async ct =>
                {
                    // FUTURE: Query GameTrendingScores table
                    // For now return mock data
                    var games = new List<TrendingGameDto>
                    {
                        new(Guid.NewGuid(), "Catan", 95.5m, 15.3m),
                        new(Guid.NewGuid(), "Wingspan", 88.2m, 8.7m),
                        new(Guid.NewGuid(), "Azul", 82.1m, -2.1m)
                    };

                    return new TrendingGamesResponseDto(games, DateTime.UtcNow, query.Period);
                },
                new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromHours(24),
                    LocalCacheExpiration = TimeSpan.FromHours(1)
                },
                tags: ["trending-games", $"period:{query.Period}"],
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            return cached;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get trending games for period {Period}", query.Period);
            return new TrendingGamesResponseDto(Array.Empty<TrendingGameDto>(), DateTime.UtcNow, query.Period);
        }
    }
}
