using Api.Models;
using Microsoft.Extensions.Options;

namespace Api.Services;

/// <summary>
/// Calculates dynamic Time-To-Live (TTL) for cache entries based on access frequency.
/// AI-10: Cache Optimization - Implements hot/warm/cold query classification for optimal cache retention.
/// Hot queries (frequently accessed) get longer TTLs to maximize cache hit rate.
/// Cold queries (rarely accessed) get shorter TTLs to avoid memory waste.
/// </summary>
internal class DynamicTtlStrategy : IDynamicTtlStrategy
{
    private readonly ILogger<DynamicTtlStrategy> _logger;
    private readonly CacheOptimizationConfiguration _config;

    /// <summary>
    /// Initializes a new instance of the DynamicTtlStrategy service.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <param name="config">Cache optimization configuration with threshold and TTL values</param>
    public DynamicTtlStrategy(
        ILogger<DynamicTtlStrategy> logger,
        IOptions<CacheOptimizationConfiguration> config)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
    }

    /// <inheritdoc />
    public Task<TimeSpan> CalculateTtlAsync(int hitCount)
    {
        // Validation: Reject negative hit counts
        if (hitCount < 0)
        {
            throw new ArgumentException("hitCount cannot be negative", nameof(hitCount));
        }

        // Classification logic (boundaries are inclusive):
        // Hot: hitCount >= HotQueryThreshold
        if (hitCount >= _config.HotQueryThreshold)
        {
            var ttl = TimeSpan.FromHours(_config.HotQueryTtlHours);
            _logger.LogDebug(
                "Query classified as HOT (hitCount={HitCount} >= {Threshold}), TTL={TTL}h",
                hitCount, _config.HotQueryThreshold, _config.HotQueryTtlHours);
            return Task.FromResult(ttl);
        }

        // Warm: hitCount >= WarmQueryThreshold (and < HotQueryThreshold)
        if (hitCount >= _config.WarmQueryThreshold)
        {
            var ttl = TimeSpan.FromHours(_config.WarmQueryTtlHours);
            _logger.LogDebug(
                "Query classified as WARM (hitCount={HitCount} in range [{WarmThreshold}, {HotThreshold})), TTL={TTL}h",
                hitCount, _config.WarmQueryThreshold, _config.HotQueryThreshold, _config.WarmQueryTtlHours);
            return Task.FromResult(ttl);
        }

        // Cold: hitCount < WarmQueryThreshold (default for first-time queries)
        var coldTtl = TimeSpan.FromHours(_config.ColdQueryTtlHours);
        _logger.LogDebug(
            "Query classified as COLD (hitCount={HitCount} < {Threshold}), TTL={TTL}h",
            hitCount, _config.WarmQueryThreshold, _config.ColdQueryTtlHours);
        return Task.FromResult(coldTtl);
    }
}
