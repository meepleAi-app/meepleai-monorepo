using System.Diagnostics.Metrics;
using Api.Models;
using Api.Observability;
using Microsoft.Extensions.Options;

namespace Api.Services;

/// <summary>
/// Records cache operation metrics (hits, misses, evictions) to OpenTelemetry.
/// AI-10: Cache Optimization - Implements fire-and-forget pattern to avoid blocking cache operations.
/// Uses existing MeepleAiMetrics static infrastructure (OPS-02) for OTEL export to Prometheus.
/// </summary>
internal class CacheMetricsRecorder : ICacheMetricsRecorder
{
    private readonly ILogger<CacheMetricsRecorder> _logger;
    private readonly CacheOptimizationConfiguration _config;

    /// <summary>
    /// Initializes a new instance of the CacheMetricsRecorder service.
    /// Uses static MeepleAiMetrics counters (CacheHitsTotal, CacheMissesTotal, CacheEvictionsTotal).
    /// </summary>
    /// <param name="logger">Logger for error handling</param>
    /// <param name="config">Cache optimization configuration (includes MetricsEnabled flag)</param>
    public CacheMetricsRecorder(
        ILogger<CacheMetricsRecorder> logger,
        IOptions<CacheOptimizationConfiguration> config)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
    }

    /// <inheritdoc />
    public Task RecordCacheHitAsync(string operation, string cacheType)
    {
        // Fire-and-forget: Don't block cache operation
        _ = Task.Run(() =>
        {
            try
            {
                // Feature flag: Skip if metrics disabled
                if (!_config.MetricsEnabled)
                    return;

                // Increment OTEL counter with labels using static MeepleAiMetrics
                var tags = new KeyValuePair<string, object?>[]
                {
                    new("operation", operation),
                    new("cache_type", cacheType)
                };

                MeepleAiMetrics.CacheHitsTotal.Add(1, tags);
            }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
            // BACKGROUND SERVICE: Fire-and-forget metrics recording must not break cache operations, telemetry is non-critical
#pragma warning restore S125
            catch (Exception ex)
#pragma warning restore CA1031
            {
                // Observability: Metrics recording must not break application functionality
                // Fire-and-forget pattern - metrics are non-critical, fail silently
                // Log error but don't throw - cache operations must not break
                _logger.LogError(ex, "Failed to record cache hit metric for operation={Operation}, cacheType={CacheType}",
                    operation, cacheType);
            }
        });

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RecordCacheMissAsync(string operation, string cacheType)
    {
        // Fire-and-forget: Don't block cache operation
        _ = Task.Run(() =>
        {
            try
            {
                if (!_config.MetricsEnabled)
                    return;

                var tags = new KeyValuePair<string, object?>[]
                {
                    new("operation", operation),
                    new("cache_type", cacheType)
                };

                MeepleAiMetrics.CacheMissesTotal.Add(1, tags);
            }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
            // BACKGROUND SERVICE: Fire-and-forget metrics recording must not break cache operations, telemetry is non-critical
#pragma warning restore S125
            catch (Exception ex)
#pragma warning restore CA1031
            {
                // Observability: Metrics recording must not break application functionality
                // Fire-and-forget pattern - metrics are non-critical, fail silently
                _logger.LogError(ex, "Failed to record cache miss metric for operation={Operation}, cacheType={CacheType}",
                    operation, cacheType);
            }
        });

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RecordCacheEvictionAsync(string reason)
    {
        // Fire-and-forget: Don't block cache operation
        _ = Task.Run(() =>
        {
            try
            {
                if (!_config.MetricsEnabled)
                    return;

                var tags = new KeyValuePair<string, object?>[]
                {
                    new("reason", reason)
                };

                MeepleAiMetrics.CacheEvictionsTotal.Add(1, tags);
            }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
            // BACKGROUND SERVICE: Fire-and-forget metrics recording must not break cache operations, telemetry is non-critical
#pragma warning restore S125
            catch (Exception ex)
#pragma warning restore CA1031
            {
                // Observability: Metrics recording must not break application functionality
                // Fire-and-forget pattern - metrics are non-critical, fail silently
                _logger.LogError(ex, "Failed to record cache eviction metric for reason={Reason}", reason);
            }
        });

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RecordCachePromotionAsync(string fromTier, string toTier)
    {
        _ = Task.Run(() =>
        {
            try
            {
                if (!_config.MetricsEnabled)
                    return;

                var tags = new KeyValuePair<string, object?>[]
                {
                    new("from_tier", fromTier),
                    new("to_tier", toTier)
                };

                MeepleAiMetrics.CachePromotionsTotal.Add(1, tags);
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "Failed to record cache promotion metric from {FromTier} to {ToTier}",
                    fromTier, toTier);
            }
        });

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RecordAdaptiveTtlAsync(string classification, double ttlSeconds)
    {
        _ = Task.Run(() =>
        {
            try
            {
                if (!_config.MetricsEnabled)
                    return;

                var tags = new KeyValuePair<string, object?>[]
                {
                    new("classification", classification),
                    new("ttl_seconds", ttlSeconds)
                };

                MeepleAiMetrics.CacheTtlAdjustmentsTotal.Add(1, tags);
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "Failed to record adaptive TTL metric for classification={Classification}",
                    classification);
            }
        });

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RecordCacheLatencyAsync(string operation, double latencyMs)
    {
        _ = Task.Run(() =>
        {
            try
            {
                if (!_config.MetricsEnabled)
                    return;

                var tags = new KeyValuePair<string, object?>[]
                {
                    new("operation", operation)
                };

                MeepleAiMetrics.CacheOperationLatency.Record(latencyMs, tags);
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "Failed to record cache latency metric for operation={Operation}",
                    operation);
            }
        });

        return Task.CompletedTask;
    }
}
