using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;

/// <summary>
/// Handler for <see cref="GetCertificationThresholdsQuery"/>. Returns the current
/// <see cref="CertificationThresholds"/> VO from the singleton
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.CertificationThresholdsConfig"/>
/// aggregate, cached for 30 minutes via <see cref="IHybridCacheService"/> with
/// a fallback to a direct repository call when the cache layer fails.
/// </summary>
/// <remarks>
/// ADR-051 Sprint 1, Phase 6 / Task 31. Mirrors the wrapper-record + try/catch
/// fallback pattern established by <see cref="GetGoldenForGameQueryHandler"/>,
/// <see cref="GetDashboardQueryHandler"/>, and
/// <see cref="GetTrendQueryHandler"/>. The 30-minute TTL is intentionally
/// longer than the dashboard/trend queries (5 minutes) because the thresholds
/// singleton mutates only via the explicit admin-driven
/// <c>UpdateCertificationThresholdsCommand</c> path, never through background
/// projection writes. Tags are fully static (<c>mechanic-validation-thresholds</c>)
/// so the array is hoisted to a static-readonly field.
/// </remarks>
internal sealed class GetCertificationThresholdsQueryHandler
    : IQueryHandler<GetCertificationThresholdsQuery, CertificationThresholds>
{
    /// <summary>
    /// Wrapper required because <see cref="IHybridCacheService.GetOrCreateAsync{T}"/>
    /// has a <c>where T : class</c> constraint. The repository contract guarantees
    /// non-null, so the wrapper exists purely for sibling symmetry and to make the
    /// stampede-protection contract identical across the validation query family.
    /// Internal (not private) so unit tests can configure the cache mock with a typed
    /// <c>Func&lt;CancellationToken, Task&lt;CachedThresholdsResult&gt;&gt;</c> matcher.
    /// </summary>
    internal sealed record CachedThresholdsResult(CertificationThresholds Thresholds);

    private static readonly string[] CacheTags = ["mechanic-validation-thresholds"];

    private readonly ICertificationThresholdsConfigRepository _configRepository;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetCertificationThresholdsQueryHandler> _logger;

    public GetCertificationThresholdsQueryHandler(
        ICertificationThresholdsConfigRepository configRepository,
        IHybridCacheService cache,
        ILogger<GetCertificationThresholdsQueryHandler> logger)
    {
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CertificationThresholds> Handle(
        GetCertificationThresholdsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug("Loading current certification thresholds singleton");

        const string cacheKey = "meepleai:mechanic-validation:thresholds";

        try
        {
            var cached = await _cache.GetOrCreateAsync(
                cacheKey,
                async ct =>
                {
                    var thresholds = await LoadThresholdsAsync(ct).ConfigureAwait(false);
                    return new CachedThresholdsResult(thresholds);
                },
                CacheTags,
                TimeSpan.FromMinutes(30),
                cancellationToken).ConfigureAwait(false);

            return cached.Thresholds;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Cache operation failed for certification thresholds singleton. Falling back to direct DB query.");

            return await LoadThresholdsAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<CertificationThresholds> LoadThresholdsAsync(CancellationToken cancellationToken)
    {
        var config = await _configRepository
            .GetAsync(cancellationToken)
            .ConfigureAwait(false);

        return config.Thresholds;
    }
}
