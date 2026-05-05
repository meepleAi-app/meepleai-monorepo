using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;

/// <summary>
/// Handler for <see cref="GetTrendQuery"/>. Returns the descending-by-time list of
/// <see cref="MechanicAnalysisMetrics"/> snapshots for a shared game, cached for
/// 5 minutes via <see cref="IHybridCacheService"/> with a fallback to a direct
/// repository call when the cache layer fails.
/// </summary>
/// <remarks>
/// ADR-051 Sprint 1, Phase 6 / Task 30. Mirrors the wrapper-record + try/catch
/// fallback pattern established by <see cref="GetGoldenForGameQueryHandler"/>,
/// <see cref="GetGoldenVersionHashQueryHandler"/>, and
/// <see cref="GetDashboardQueryHandler"/>. The 5-minute TTL matches the dashboard
/// because trend rows are appended on every metrics computation; tags are
/// <c>game:{sharedGameId}</c> (per-game targeted bust on metrics writes) plus the
/// broad <c>mechanic-validation-trend</c> tag (nuclear invalidation if shape
/// changes). Tags include a runtime <c>sharedGameId</c>, so the array is
/// allocated per-call rather than hoisted to a static field.
/// </remarks>
internal sealed class GetTrendQueryHandler
    : IQueryHandler<GetTrendQuery, IReadOnlyList<MechanicAnalysisMetrics>>
{
    /// <summary>
    /// Wrapper required because <see cref="IHybridCacheService.GetOrCreateAsync{T}"/>
    /// has a <c>where T : class</c> constraint and we need to cache an empty-list
    /// result without null ambiguity. Internal (not private) so unit tests can
    /// configure the cache mock with a typed
    /// <c>Func&lt;CancellationToken, Task&lt;CachedTrendResult&gt;&gt;</c> matcher.
    /// </summary>
    internal sealed record CachedTrendResult(IReadOnlyList<MechanicAnalysisMetrics> Snapshots);

    private readonly IMechanicAnalysisMetricsRepository _metricsRepository;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetTrendQueryHandler> _logger;

    public GetTrendQueryHandler(
        IMechanicAnalysisMetricsRepository metricsRepository,
        IHybridCacheService cache,
        ILogger<GetTrendQueryHandler> logger)
    {
        _metricsRepository = metricsRepository ?? throw new ArgumentNullException(nameof(metricsRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<MechanicAnalysisMetrics>> Handle(
        GetTrendQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug(
            "Loading mechanic validation trend for SharedGameId={SharedGameId} Take={Take}",
            request.SharedGameId,
            request.Take);

        var cacheKey = $"meepleai:mechanic-validation:trend:{request.SharedGameId}:{request.Take}";
        var tags = new[] { $"game:{request.SharedGameId}", "mechanic-validation-trend" };

        try
        {
            var cached = await _cache.GetOrCreateAsync(
                cacheKey,
                async ct =>
                {
                    var snapshots = await LoadTrendAsync(request.SharedGameId, request.Take, ct).ConfigureAwait(false);
                    return new CachedTrendResult(snapshots);
                },
                tags,
                TimeSpan.FromMinutes(5),
                cancellationToken).ConfigureAwait(false);

            return cached.Snapshots;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Cache operation failed for mechanic validation trend (SharedGameId={SharedGameId}, Take={Take}). Falling back to direct DB query.",
                request.SharedGameId,
                request.Take);

            return await LoadTrendAsync(request.SharedGameId, request.Take, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyList<MechanicAnalysisMetrics>> LoadTrendAsync(
        Guid sharedGameId,
        int take,
        CancellationToken cancellationToken)
    {
        return await _metricsRepository
            .GetTrendAsync(sharedGameId, take, cancellationToken)
            .ConfigureAwait(false);
    }
}
