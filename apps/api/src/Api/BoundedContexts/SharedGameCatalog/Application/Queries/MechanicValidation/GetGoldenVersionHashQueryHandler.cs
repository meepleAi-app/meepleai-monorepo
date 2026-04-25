using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;

/// <summary>
/// Handler for <see cref="GetGoldenVersionHashQuery"/>. Returns the current golden-set
/// version hash for a shared game, cached for 10 minutes via <see cref="IHybridCacheService"/>
/// with a fallback to direct DB lookup when the cache layer fails.
/// </summary>
/// <remarks>
/// ADR-051 Sprint 1, Phase 6 / Task 28. Mirrors the wrapper-record + try/catch fallback
/// pattern established by <see cref="GetGoldenForGameQueryHandler"/>. The 10-minute TTL
/// and shared cache tags (<c>game:{sharedGameId}</c>, <c>mechanic-golden</c>) ensure
/// invalidations propagate consistently between the full golden DTO and this lightweight
/// hash-only query.
/// </remarks>
internal sealed class GetGoldenVersionHashQueryHandler
    : IQueryHandler<GetGoldenVersionHashQuery, string>
{
    /// <summary>
    /// Wrapper required because <see cref="IHybridCacheService.GetOrCreateAsync{T}"/> has a
    /// <c>where T : class</c> constraint and we need to cache the "no claims yet" empty-string
    /// result without null ambiguity. Internal (not private) so unit tests can configure the
    /// cache mock with a typed
    /// <c>Func&lt;CancellationToken, Task&lt;CachedVersionHashResult&gt;&gt;</c> matcher.
    /// </summary>
    internal sealed record CachedVersionHashResult(string Value);

    private readonly IMechanicGoldenClaimRepository _claimRepository;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetGoldenVersionHashQueryHandler> _logger;

    public GetGoldenVersionHashQueryHandler(
        IMechanicGoldenClaimRepository claimRepository,
        IHybridCacheService cache,
        ILogger<GetGoldenVersionHashQueryHandler> logger)
    {
        _claimRepository = claimRepository ?? throw new ArgumentNullException(nameof(claimRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> Handle(
        GetGoldenVersionHashQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug(
            "Loading golden-set version hash for game {SharedGameId}",
            request.SharedGameId);

        var cacheKey = $"meepleai:mechanic-golden:version-hash:{request.SharedGameId}";
        var cacheTags = new[] { $"game:{request.SharedGameId}", "mechanic-golden" };

        try
        {
            var cached = await _cache.GetOrCreateAsync(
                cacheKey,
                async ct =>
                {
                    var value = await LoadVersionHashAsync(request.SharedGameId, ct).ConfigureAwait(false);
                    return new CachedVersionHashResult(value);
                },
                cacheTags,
                TimeSpan.FromMinutes(10),
                cancellationToken).ConfigureAwait(false);

            return cached.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Cache operation failed for golden-set version hash on game {SharedGameId}. Falling back to direct DB query.",
                request.SharedGameId);

            return await LoadVersionHashAsync(request.SharedGameId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<string> LoadVersionHashAsync(Guid sharedGameId, CancellationToken cancellationToken)
    {
        var versionHash = await _claimRepository
            .GetVersionHashAsync(sharedGameId, cancellationToken)
            .ConfigureAwait(false);

        return versionHash?.Value ?? string.Empty;
    }
}
