using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;

/// <summary>
/// Handler for <see cref="GetDashboardQuery"/>. Returns the per-game summary rows
/// for the admin validation dashboard, cached for 5 minutes via
/// <see cref="IHybridCacheService"/> with a fallback to direct DB query when the
/// cache layer fails.
/// </summary>
/// <remarks>
/// ADR-051 Sprint 1, Phase 6 / Task 29. Mirrors the wrapper-record + try/catch
/// fallback pattern established by <see cref="GetGoldenForGameQueryHandler"/> and
/// <see cref="GetGoldenVersionHashQueryHandler"/>. The 5-minute TTL is tighter
/// than the 10-minute TTL used by golden-set queries because dashboard data is
/// more volatile (certification grants, override decisions, and new metrics
/// snapshots all invalidate the projection). Tagged with <c>mechanic-golden</c>
/// so golden-set updates also bust this cache, plus the scoped
/// <c>mechanic-validation-dashboard</c> tag for targeted invalidation from the
/// metrics-computation and certification-override handlers.
/// </remarks>
internal sealed class GetDashboardQueryHandler
    : IQueryHandler<GetDashboardQuery, IReadOnlyList<DashboardGameRow>>
{
    /// <summary>
    /// Wrapper required because <see cref="IHybridCacheService.GetOrCreateAsync{T}"/>
    /// has a <c>where T : class</c> constraint and we need to cache an empty-list
    /// result without null ambiguity. Internal (not private) so unit tests can
    /// configure the cache mock with a typed
    /// <c>Func&lt;CancellationToken, Task&lt;CachedDashboardResult&gt;&gt;</c> matcher.
    /// </summary>
    internal sealed record CachedDashboardResult(IReadOnlyList<DashboardGameRow> Rows);

    private readonly IMechanicAnalysisMetricsRepository _metricsRepository;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetDashboardQueryHandler> _logger;

    public GetDashboardQueryHandler(
        IMechanicAnalysisMetricsRepository metricsRepository,
        IHybridCacheService cache,
        ILogger<GetDashboardQueryHandler> logger)
    {
        _metricsRepository = metricsRepository ?? throw new ArgumentNullException(nameof(metricsRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<DashboardGameRow>> Handle(
        GetDashboardQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug("Loading mechanic validation dashboard summary");

        const string cacheKey = "meepleai:mechanic-validation:dashboard";
        var cacheTags = new[] { "mechanic-golden", "mechanic-validation-dashboard" };

        try
        {
            var cached = await _cache.GetOrCreateAsync(
                cacheKey,
                async ct =>
                {
                    var rows = await LoadDashboardAsync(ct).ConfigureAwait(false);
                    return new CachedDashboardResult(rows);
                },
                cacheTags,
                TimeSpan.FromMinutes(5),
                cancellationToken).ConfigureAwait(false);

            return cached.Rows;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Cache operation failed for mechanic validation dashboard. Falling back to direct DB query.");

            return await LoadDashboardAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyList<DashboardGameRow>> LoadDashboardAsync(CancellationToken cancellationToken)
    {
        return await _metricsRepository
            .GetDashboardAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
