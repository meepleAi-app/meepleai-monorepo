# AI-10 Cache Optimization - PromQL Queries Reference

**Purpose**: Reference document for Grafana dashboard queries and ad-hoc analysis

## Core Metrics Queries

### Cache Hit Rate (Percentage)

```promql
# Hit rate over last 5 minutes (%)
rate(meepleai_cache_hits_total[5m]) /
(rate(meepleai_cache_hits_total[5m]) + rate(meepleai_cache_misses_total[5m])) * 100

# Hit rate by key type
sum by (key_type) (rate(meepleai_cache_hits_total[5m])) /
(sum by (key_type) (rate(meepleai_cache_hits_total[5m]) + rate(meepleai_cache_misses_total[5m]))) * 100
```

### Cache Operations Rate (ops/sec)

```promql
# Total cache operations per second
rate(meepleai_cache_hits_total[5m]) + rate(meepleai_cache_misses_total[5m])

# Hits per second
rate(meepleai_cache_hits_total[5m])

# Misses per second
rate(meepleai_cache_misses_total[5m])

# Operations by key type
sum by (key_type) (rate(meepleai_cache_hits_total[5m]) + rate(meepleai_cache_misses_total[5m]))
```

### Cache Operation Latency (Percentiles)

```promql
# p50 (median) latency
histogram_quantile(0.50, rate(meepleai_cache_operation_duration_bucket[5m]))

# p95 latency (should be <10ms)
histogram_quantile(0.95, rate(meepleai_cache_operation_duration_bucket[5m]))

# p99 latency
histogram_quantile(0.99, rate(meepleai_cache_operation_duration_bucket[5m]))

# Average latency
rate(meepleai_cache_operation_duration_sum[5m]) /
rate(meepleai_cache_operation_duration_count[5m])

# Latency by operation type (get vs set)
histogram_quantile(0.95, sum by (operation, le) (rate(meepleai_cache_operation_duration_bucket[5m])))
```

### Cache Invalidations

```promql
# Total invalidations per second
rate(meepleai_cache_invalidations_total[5m])

# Invalidations by reason
sum by (reason) (rate(meepleai_cache_invalidations_total[5m]))

# Total keys invalidated (last hour)
increase(meepleai_cache_invalidations_total[1h])

# Invalidations by reason (pie chart)
sum by (reason) (increase(meepleai_cache_invalidations_total[1h]))
```

### Metrics Recording Errors

```promql
# Metrics errors per second
rate(meepleai_cache_metrics_errors_total[5m])

# Error rate as percentage of operations
rate(meepleai_cache_metrics_errors_total[5m]) /
(rate(meepleai_cache_hits_total[5m]) + rate(meepleai_cache_misses_total[5m])) * 100

# Total errors (last 24 hours)
increase(meepleai_cache_metrics_errors_total[24h])
```

---

## Comparative Analysis Queries

### Before/After Optimization Comparison

```promql
# Cache hit rate change (compare 7 days before vs 7 days after)
# Query 1: Baseline (7 days before deployment)
avg_over_time((rate(meepleai_cache_hits_total[5m]) /
(rate(meepleai_cache_hits_total[5m]) + rate(meepleai_cache_misses_total[5m])) * 100)[7d:5m] offset 7d)

# Query 2: Current (7 days after deployment)
avg_over_time((rate(meepleai_cache_hits_total[5m]) /
(rate(meepleai_cache_hits_total[5m]) + rate(meepleai_cache_misses_total[5m])) * 100)[7d:5m])
```

### Latency Regression Detection

```promql
# Alert if p95 latency >10ms
histogram_quantile(0.95, rate(meepleai_cache_operation_duration_bucket[5m])) > 10

# Compare latency before/after optimization
# Query 1: Baseline p95 (offset 7 days)
histogram_quantile(0.95, rate(meepleai_cache_operation_duration_bucket[5m] offset 7d))

# Query 2: Current p95
histogram_quantile(0.95, rate(meepleai_cache_operation_duration_bucket[5m]))
```

---

## Key Type Analysis

### Top Key Types by Hit Count

```promql
# Top 10 key types by hits (last 1 hour)
topk(10, sum by (key_type) (increase(meepleai_cache_hits_total[1h])))
```

### Key Type Hit Rates

```promql
# Hit rate by key type (table format)
sum by (key_type) (rate(meepleai_cache_hits_total[5m])) /
(sum by (key_type) (rate(meepleai_cache_hits_total[5m]) + rate(meepleai_cache_misses_total[5m]))) * 100
```

### Key Type Distribution (Pie Charts)

```promql
# Cache hits distribution by key type
sum by (key_type) (increase(meepleai_cache_hits_total[1h]))

# Cache misses distribution by key type
sum by (key_type) (increase(meepleai_cache_misses_total[1h]))
```

---

## Dynamic TTL Analysis

### TTL Distribution Over Time

```promql
# Note: TTL is set at cache.set time, not tracked by default metrics
# Would require custom histogram metric: meepleai_cache_ttl_seconds_bucket

# If implemented:
histogram_quantile(0.50, rate(meepleai_cache_ttl_seconds_bucket[5m]))  # Median TTL
histogram_quantile(0.95, rate(meepleai_cache_ttl_seconds_bucket[5m]))  # p95 TTL
```

### Average TTL by Key Type

```promql
# If TTL histogram metric implemented:
avg by (key_type) (rate(meepleai_cache_ttl_seconds_sum[5m]) /
rate(meepleai_cache_ttl_seconds_count[5m]))
```

---

## Cache Warming Analysis

### Warming Success Rate

```promql
# Note: Cache warming metrics tracked via logs, not Prometheus
# Use LogQL in Grafana Loki instead (if integrated):

# LogQL query (Loki):
{job="meepleai-api"} |= "CacheWarmingService" |= "Completed" | logfmt |
  __error__="" |
  line_format "{{.queriesWarmed}} / {{.topQueriesCount}}"
```

### Warming Duration Trends

```promql
# Note: Use LogQL for warming duration analysis

# LogQL query (Loki):
{job="meepleai-api"} |= "CacheWarmingService" |= "duration" | logfmt |
  __error__="" |
  unwrap durationSeconds [5m] |
  avg_over_time(durationSeconds[5m])
```

---

## Redis Memory Analysis

### Redis Memory Usage

```promql
# Redis memory used (MB)
redis_memory_used_bytes / 1024 / 1024

# Redis memory usage percentage
(redis_memory_used_bytes / redis_memory_max_bytes) * 100

# Memory growth rate (MB/hour)
rate(redis_memory_used_bytes[1h]) / 1024 / 1024 * 3600
```

### Cache Data Size Estimation

```promql
# Total cache keys (requires custom metric: meepleai_cache_keys_total)
# If implemented:
sum(meepleai_cache_keys_total)

# Memory per key (if both metrics available)
redis_memory_used_bytes / sum(meepleai_cache_keys_total)
```

---

## Alerting Queries

### Critical Alerts (Pager)

```promql
# Alert: Cache hit rate drops below 50%
(rate(meepleai_cache_hits_total[5m]) /
(rate(meepleai_cache_hits_total[5m]) + rate(meepleai_cache_misses_total[5m])) * 100) < 50

# Alert: Cache operation latency p95 >10ms
histogram_quantile(0.95, rate(meepleai_cache_operation_duration_bucket[5m])) > 10

# Alert: Redis memory >80%
(redis_memory_used_bytes / redis_memory_max_bytes) * 100 > 80
```

### Warning Alerts (Email)

```promql
# Alert: Metrics recording error rate >1%
(rate(meepleai_cache_metrics_errors_total[5m]) /
(rate(meepleai_cache_hits_total[5m]) + rate(meepleai_cache_misses_total[5m])) * 100) > 1

# Alert: High invalidation rate (>10/sec)
rate(meepleai_cache_invalidations_total[5m]) > 10

# Alert: Cache warming failure rate >5% (requires custom metric)
# (rate(meepleai_cache_warming_errors_total[5m]) /
# rate(meepleai_cache_warming_attempts_total[5m])) * 100 > 5
```

---

## Ad-Hoc Analysis Queries

### Frequency Tracking Overhead

```promql
# Compare latency with/without frequency tracking
# (Requires A/B testing with feature flag or time-based comparison)

# Before frequency tracking enabled:
avg(histogram_quantile(0.95, rate(meepleai_cache_operation_duration_bucket[5m] offset 7d)))

# After frequency tracking enabled:
avg(histogram_quantile(0.95, rate(meepleai_cache_operation_duration_bucket[5m])))
```

### Cache Effectiveness by Time of Day

```promql
# Hit rate by hour (24h heatmap)
rate(meepleai_cache_hits_total[1h]) /
(rate(meepleai_cache_hits_total[1h]) + rate(meepleai_cache_misses_total[1h])) * 100

# Operations by hour (identify peak traffic)
sum(rate(meepleai_cache_hits_total[1h]) + rate(meepleai_cache_misses_total[1h]))
```

### Cost-Benefit Analysis

```promql
# Estimated cost savings from cache hits (assuming 100ms RAG query cost vs 1ms cache hit)
# Queries saved per second
rate(meepleai_cache_hits_total[5m])

# Time saved per day (hours)
(rate(meepleai_cache_hits_total[5m]) * 0.099) * 86400 / 3600

# Where 0.099 = (100ms RAG - 1ms cache) = 99ms saved per hit
```

---

## Custom Metrics (Future Enhancements)

### Proposed Additional Metrics

If these metrics are added in the future, use these queries:

#### Cache Warming Metrics

```promql
# Cache warming success rate
rate(meepleai_cache_warming_success_total[5m]) /
rate(meepleai_cache_warming_attempts_total[5m]) * 100

# Cache warming duration p95
histogram_quantile(0.95, rate(meepleai_cache_warming_duration_bucket[5m]))
```

#### Frequency Tracking Metrics

```promql
# Frequency increment rate (ops/sec)
rate(meepleai_cache_frequency_increments_total[5m])

# Frequency ZSET size (keys per game)
meepleai_cache_frequency_zset_size
```

#### TTL Metrics

```promql
# Average TTL set (seconds)
rate(meepleai_cache_ttl_seconds_sum[5m]) / rate(meepleai_cache_ttl_seconds_count[5m])

# TTL by strategy (if multiple strategies)
avg by (ttl_strategy) (rate(meepleai_cache_ttl_seconds_sum[5m]) /
rate(meepleai_cache_ttl_seconds_count[5m]))
```

---

## Dashboard Panel Recommendations

### Panel 1: Cache Hit Rate (Graph)
- **Query**: `rate(meepleai_cache_hits_total[5m]) / (rate(meepleai_cache_hits_total[5m]) + rate(meepleai_cache_misses_total[5m])) * 100`
- **Visualization**: Time series graph
- **Thresholds**: <50% (red), 50-70% (yellow), >70% (green)
- **Y-axis**: 0-100%

### Panel 2: Cache Operations Rate (Graph)
- **Queries**:
  - Hits: `rate(meepleai_cache_hits_total[5m])`
  - Misses: `rate(meepleai_cache_misses_total[5m])`
- **Visualization**: Stacked area graph
- **Y-axis**: ops/sec

### Panel 3: Cache Latency (Graph)
- **Queries**:
  - p50: `histogram_quantile(0.50, rate(meepleai_cache_operation_duration_bucket[5m]))`
  - p95: `histogram_quantile(0.95, rate(meepleai_cache_operation_duration_bucket[5m]))`
  - p99: `histogram_quantile(0.99, rate(meepleai_cache_operation_duration_bucket[5m]))`
- **Visualization**: Time series graph with thresholds
- **Thresholds**: 5ms (yellow), 10ms (red)
- **Y-axis**: Milliseconds

### Panel 4: Key Type Distribution (Pie Chart)
- **Query**: `sum by (key_type) (increase(meepleai_cache_hits_total[1h]))`
- **Visualization**: Pie chart
- **Legend**: Show percentages

### Panel 5: Redis Memory Usage (Graph)
- **Queries**:
  - Used: `redis_memory_used_bytes / 1024 / 1024`
  - Max: `redis_memory_max_bytes / 1024 / 1024`
- **Visualization**: Time series graph with threshold line
- **Threshold**: 80% of max (red)
- **Y-axis**: Megabytes

### Panel 6: Cache Invalidations (Counter)
- **Query**: `increase(meepleai_cache_invalidations_total[1h])`
- **Visualization**: Stat panel
- **Sparkline**: Show trend

### Panel 7: Metrics Errors (Stat)
- **Query**: `rate(meepleai_cache_metrics_errors_total[5m])`
- **Visualization**: Stat panel with thresholds
- **Thresholds**: 0 (green), >0.01 (yellow), >0.1 (red)
- **Unit**: ops/sec

---

## Troubleshooting Queries

### No Data in Dashboard

```promql
# Check if metrics are being collected
up{job="meepleai-api"}

# Check if cache metrics exist
count(meepleai_cache_hits_total)

# Check last scrape time
time() - timestamp(meepleai_cache_hits_total)
```

### High Latency Investigation

```promql
# Latency by operation type
histogram_quantile(0.95, sum by (operation, le) (rate(meepleai_cache_operation_duration_bucket[5m])))

# Latency by key type
histogram_quantile(0.95, sum by (key_type, le) (rate(meepleai_cache_operation_duration_bucket[5m])))

# Latency outliers (slowest operations)
topk(10, rate(meepleai_cache_operation_duration_sum[5m]) /
rate(meepleai_cache_operation_duration_count[5m]))
```

### Low Hit Rate Investigation

```promql
# Hit rate by key type (identify problematic types)
sum by (key_type) (rate(meepleai_cache_hits_total[5m])) /
(sum by (key_type) (rate(meepleai_cache_hits_total[5m]) + rate(meepleai_cache_misses_total[5m]))) * 100

# Cache eviction rate (if metric exists)
rate(meepleai_cache_evictions_total[5m])

# Invalidation rate (may cause low hit rate)
rate(meepleai_cache_invalidations_total[5m])
```

---

## References

- **Grafana Dashboard JSON**: `infra/dashboards/cache-optimization.json`
- **Prometheus Endpoint**: `http://localhost:8080/metrics`
- **Grafana UI**: `http://localhost:3001` (admin/admin)
- **PromQL Documentation**: https://prometheus.io/docs/prometheus/latest/querying/basics/
- **MeepleAI Metrics**: `apps/api/src/Api/Observability/MeepleAiMetrics.cs`

---

## Notes

- **Rate Window**: Most queries use `[5m]` window for rate calculations (balances responsiveness vs noise)
- **Increase vs Rate**: Use `rate()` for per-second rates, `increase()` for total counts over period
- **Histogram Quantiles**: Always use `rate()` with histogram buckets before `histogram_quantile()`
- **Offset**: Use `offset 7d` to compare current vs baseline (e.g., before/after optimization)
- **Thresholds**: Adjust thresholds based on your SLA requirements (<5ms target may vary)
