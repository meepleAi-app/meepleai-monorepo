# Session Cache Metrics Monitoring

## Overview

Next.js middleware session cache metrics for monitoring authentication performance and cache effectiveness.

**Created**: Issue #3797 - Post-resolution improvements
**Endpoint**: `http://localhost:3000/metrics`
**Scrape Interval**: 15s (Prometheus)

---

## Available Metrics

### Cache Performance

| Metric | Type | Description |
|--------|------|-------------|
| `nextjs_middleware_cache_hit_total` | counter | Total cache hits |
| `nextjs_middleware_cache_miss_total` | counter | Total cache misses |
| `nextjs_middleware_cache_hit_rate` | gauge | Cache hit rate (0-1) |

### Session Validation

| Metric | Type | Description |
|--------|------|-------------|
| `nextjs_middleware_validation_success_total` | counter | Successful validations |
| `nextjs_middleware_validation_failure_total` | counter | Failed validations |
| `nextjs_middleware_validation_timeout_total` | counter | Timeout occurrences |
| `nextjs_middleware_validation_success_rate` | gauge | Success rate (0-1) |

### System

| Metric | Type | Description |
|--------|------|-------------|
| `nextjs_middleware_uptime_seconds` | counter | Middleware uptime |
| `nextjs_middleware_total_requests` | counter | Total requests processed |

---

## Prometheus Queries

### Cache Hit Rate (%)
```promql
nextjs_middleware_cache_hit_rate * 100
```

**Expected**: >80% after warmup (with 120s TTL)

### Cache Miss Rate per Second
```promql
rate(nextjs_middleware_cache_miss_total[5m])
```

**Alert if**: >10 misses/sec (indicates cache inefficiency)

### Validation Timeout Rate
```promql
rate(nextjs_middleware_validation_timeout_total[5m])
```

**Alert if**: >0 (indicates API performance issues)

### Validation Error Rate (%)
```promql
(
  nextjs_middleware_validation_failure_total +
  nextjs_middleware_validation_timeout_total
) / (
  nextjs_middleware_validation_success_total +
  nextjs_middleware_validation_failure_total +
  nextjs_middleware_validation_timeout_total
) * 100
```

**Alert if**: >5% (indicates authentication issues)

### Total Middleware Requests per Second
```promql
rate(nextjs_middleware_total_requests[1m])
```

**Typical**: 1-10 req/sec in dev, 10-100 req/sec in production

---

## Grafana Dashboard

### Panel 1: Cache Performance

**Query**:
```promql
# Cache Hit Rate (%)
nextjs_middleware_cache_hit_rate * 100
```

**Visualization**: Gauge
**Thresholds**:
- 🟢 Green: >80%
- 🟡 Yellow: 60-80%
- 🔴 Red: <60%

### Panel 2: Cache Operations

**Queries**:
```promql
# Cache Hits
rate(nextjs_middleware_cache_hit_total[5m])

# Cache Misses
rate(nextjs_middleware_cache_miss_total[5m])
```

**Visualization**: Time series (stacked area)
**Legend**: Hits (green), Misses (orange)

### Panel 3: Validation Status

**Queries**:
```promql
# Success
rate(nextjs_middleware_validation_success_total[5m])

# Failures
rate(nextjs_middleware_validation_failure_total[5m])

# Timeouts
rate(nextjs_middleware_validation_timeout_total[5m])
```

**Visualization**: Time series (stacked bar)
**Legend**: Success (green), Failure (red), Timeout (orange)

### Panel 4: System Health

**Queries**:
```promql
# Uptime
nextjs_middleware_uptime_seconds / 3600

# Total Requests
rate(nextjs_middleware_total_requests[1m]) * 60
```

**Visualization**: Stat panels
**Units**: Hours (uptime), Requests/min

---

## Alert Rules

Add to `infra/prometheus-rules.yml`:

```yaml
groups:
  - name: nextjs_middleware
    interval: 30s
    rules:
      # Cache hit rate too low
      - alert: MiddlewareCacheHitRateLow
        expr: nextjs_middleware_cache_hit_rate < 0.6
        for: 5m
        labels:
          severity: warning
          component: frontend
        annotations:
          summary: "Low middleware cache hit rate"
          description: "Cache hit rate is {{ $value | humanizePercentage }}, expected >80%"

      # Validation timeouts occurring
      - alert: MiddlewareValidationTimeouts
        expr: rate(nextjs_middleware_validation_timeout_total[5m]) > 0
        for: 2m
        labels:
          severity: critical
          component: frontend
        annotations:
          summary: "Middleware session validation timeouts"
          description: "API /auth/me endpoint timing out ({{ $value }} timeouts/sec)"

      # High validation error rate
      - alert: MiddlewareValidationErrors
        expr: |
          (
            rate(nextjs_middleware_validation_failure_total[5m]) +
            rate(nextjs_middleware_validation_timeout_total[5m])
          ) / (
            rate(nextjs_middleware_validation_success_total[5m]) +
            rate(nextjs_middleware_validation_failure_total[5m]) +
            rate(nextjs_middleware_validation_timeout_total[5m])
          ) > 0.1
        for: 5m
        labels:
          severity: warning
          component: frontend
        annotations:
          summary: "High middleware validation error rate"
          description: "{{ $value | humanizePercentage }} of validations failing"
```

---

## Troubleshooting

### Metrics Not Showing in Prometheus

**Check endpoint availability**:
```bash
curl http://localhost:3000/metrics
```

**Expected output**:
```
# HELP nextjs_middleware_cache_hit_total Total number of session cache hits
# TYPE nextjs_middleware_cache_hit_total counter
nextjs_middleware_cache_hit_total 150
...
```

**If 404**: Ensure Next.js container rebuilt with new route

**If timeout**: Check web container health:
```bash
docker compose ps web
docker compose logs web
```

### Prometheus Not Scraping

**Check Prometheus targets**:
- Open: http://localhost:9090/targets
- Find: `meepleai-web` job
- Status should be: UP

**If DOWN**: Check network connectivity:
```bash
docker compose exec prometheus curl http://web:3000/metrics
```

### Metrics Reset to Zero

**Cause**: Container restart (metrics are in-memory)

**Solution**: Expected behavior - metrics reset on deployment

**Future**: Consider persistent metrics via Redis/persistent storage

---

## Performance Baselines

### Healthy Metrics (Development)

| Metric | Expected Value | Alert Threshold |
|--------|----------------|-----------------|
| Cache Hit Rate | >80% | <60% |
| Validation Success Rate | >95% | <90% |
| Timeout Rate | 0/sec | >0/sec |
| Requests/sec | 1-10 | N/A |

### Production Targets

| Metric | Target | Critical |
|--------|--------|----------|
| Cache Hit Rate | >90% | <70% |
| Validation Success Rate | >99% | <95% |
| Timeout Rate | 0/sec | >0.01/sec |
| Requests/sec | 10-100 | >200 |

---

## Integration

### Prometheus Configuration

Added to `infra/prometheus.yml`:
```yaml
- job_name: 'meepleai-web'
  scrape_interval: 15s
  metrics_path: '/metrics'
  static_configs:
    - targets: ['web:3000']
```

### Middleware Implementation

Metrics collected in:
- `apps/web/src/lib/metrics/session-cache-metrics.ts`
- `apps/web/proxy.ts` (instrumentation)
- `apps/web/src/app/metrics/route.ts` (Prometheus endpoint)

---

## Related

- Issue #3797: Port conflict resolution
- ADR-XXX: Observability strategy
- `infra/prometheus-rules.yml`: Alert rules
- `infra/dashboards/`: Grafana dashboards

---

**Last Updated**: 2026-02-07
**Maintained by**: DevOps team
