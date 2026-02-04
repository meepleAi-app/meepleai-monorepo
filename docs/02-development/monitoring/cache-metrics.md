# Multi-Tier Cache Metrics Integration

**Issue**: #3494 - Redis 3-Tier Cache Layer
**Dashboard**: `infra/monitoring/grafana/dashboards/multi-tier-cache-performance.json`

## Overview

The MultiTierCache system tracks comprehensive metrics via `ICacheMetricsRecorder`. A Grafana dashboard is provided for monitoring cache performance, hit rates, and TTL distribution.

## Required Prometheus Metrics

The following metrics need to be exported by the `ICacheMetricsRecorder` implementation for the Grafana dashboard to function:

### Cache Hit/Miss Counters
```
cache_hits_total{tier="l1_memory"} counter
cache_hits_total{tier="l2_redis"} counter
cache_hits_total{tier="all"} counter
cache_misses_total{tier="all_tiers"} counter
```

### Cache Latency Histograms
```
cache_operation_latency_ms_bucket{operation="get"} histogram
cache_operation_latency_ms_bucket{operation="set"} histogram
```

**Buckets**: `[0.1, 0.5, 1, 5, 10, 25, 50, 100, 250, 500, 1000]` (milliseconds)

### Cache Entry Counts
```
cache_l1_entry_count gauge
cache_l2_entry_count gauge
```

### Promotion & Eviction Counters
```
cache_promotions_total counter
cache_evictions_total{tier="l1_memory"} counter
```

### Adaptive TTL Classification
```
cache_ttl_adjustments_total{classification="high"} counter  # 100+ hits, 24h TTL
cache_ttl_adjustments_total{classification="medium"} counter  # 10+ hits, 1h TTL
cache_ttl_adjustments_total{classification="low"} counter  # <10 hits, 5min TTL
```

## Implementation Status

| Component | Status | Notes |
|-----------|--------|-------|
| Metrics Tracking | ✅ Complete | `ICacheMetricsRecorder` called throughout MultiTierCache |
| Grafana Dashboard | ✅ Complete | JSON configuration in `infra/monitoring/grafana/dashboards/` |
| Prometheus Export | ⏳ TODO | Requires implementation in CacheMetricsRecorder |
| Dashboard Import | ⏳ TODO | Deploy dashboard to Grafana after Prometheus integration |

## Next Steps

1. **Implement Prometheus export in CacheMetricsRecorder**:
   - Use `Prometheus.Client.Metrics` NuGet package
   - Create Counter, Gauge, and Histogram metrics
   - Update `RecordCacheHitAsync`, `RecordCacheMissAsync` to increment Prometheus metrics

2. **Configure Prometheus scraping**:
   - Add `/metrics` endpoint to ASP.NET application
   - Update `infra/prometheus.yml` with scrape config

3. **Import Grafana dashboard**:
   - Deploy dashboard JSON to Grafana instance
   - Configure Prometheus datasource UID if different from "prometheus"

## Example: CacheMetricsRecorder Prometheus Integration

```csharp
using Prometheus;

public class CacheMetricsRecorder : ICacheMetricsRecorder
{
    private static readonly Counter CacheHits = Metrics.CreateCounter(
        "cache_hits_total",
        "Total cache hits by tier",
        new CounterConfiguration { LabelNames = new[] { "tier" } });

    private static readonly Counter CacheMisses = Metrics.CreateCounter(
        "cache_misses_total",
        "Total cache misses by tier",
        new CounterConfiguration { LabelNames = new[] { "tier" } });

    private static readonly Histogram CacheLatency = Metrics.CreateHistogram(
        "cache_operation_latency_ms",
        "Cache operation latency in milliseconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "operation" },
            Buckets = new[] { 0.1, 0.5, 1, 5, 10, 25, 50, 100, 250, 500, 1000 }
        });

    public Task RecordCacheHitAsync(string operation, string tier)
    {
        CacheHits.WithLabels(tier).Inc();
        return Task.CompletedTask;
    }

    public Task RecordCacheMissAsync(string operation, string tier)
    {
        CacheMisses.WithLabels(tier).Inc();
        return Task.CompletedTask;
    }
}
```

## Dashboard Panels

1. **Cache Hit Rate Gauge** - Overall hit rate with 80% target (red < 70%, yellow < 80%, green ≥ 80%)
2. **Operations Rate by Tier** - L1 hits/sec, L2 hits/sec, misses/sec
3. **Cache Latency** - P95/P99 latency for get/set operations (target: P95 < 100ms)
4. **Entry Count by Tier** - L1 and L2 entry counts (L1 max: 1000)
5. **Promotion & Eviction Rate** - Cache promotions and L1 evictions
6. **Adaptive TTL Distribution** - High/medium/low frequency classification counts

## Performance Targets

| Metric | Target | Alert Threshold |
|--------|--------|-----------------|
| Overall Hit Rate | > 80% | < 70% (critical) |
| P95 Latency | < 100ms | > 150ms (warning), > 200ms (critical) |
| L1 Entry Count | ≤ 1000 | > 950 (warning) |
| L2 Hit Rate | > 60% | < 50% (info) |
| Promotion Rate | Stable | Spikes indicate L1 thrashing |

## Alerts

Recommended Prometheus alert rules:

```yaml
groups:
  - name: cache_alerts
    rules:
      - alert: CacheHitRateLow
        expr: |
          100 * sum(rate(cache_hits_total{tier="all"}[5m]))
          / (sum(rate(cache_hits_total{tier="all"}[5m])) + sum(rate(cache_misses_total{tier="all_tiers"}[5m])))
          < 70
        for: 10m
        labels:
          severity: critical
        annotations:
          summary: "Cache hit rate below 70% for 10 minutes"
          description: "Current hit rate: {{ $value }}%. Target: >80%"

      - alert: CacheLatencyHigh
        expr: histogram_quantile(0.95, rate(cache_operation_latency_ms_bucket{operation="get"}[5m])) > 200
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Cache P95 latency exceeds 200ms"
          description: "P95 get latency: {{ $value }}ms. Target: <100ms"
```

## References

- **Implementation**: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Caching/MultiTierCache.cs`
- **Configuration**: `apps/api/src/Api/appsettings.json` (MultiTierCache section)
- **Tests**: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/Caching/*Tests.cs`
- **Architecture**: Issue #3494, Epic #3490
