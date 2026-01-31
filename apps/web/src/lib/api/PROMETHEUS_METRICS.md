# Prometheus Metrics Integration

This document describes how to integrate and expose Prometheus metrics for HTTP client monitoring.

## Available Metrics

### Request Deduplication Cache Metrics (Issue #1454)

The request cache provides the following Prometheus metrics:

| Metric Name | Type | Description |
|-------------|------|-------------|
| `http_client_cache_hits_total` | Counter | Total number of cache hits |
| `http_client_cache_misses_total` | Counter | Total number of cache misses |
| `http_client_cache_hit_rate_percent` | Gauge | Cache hit rate percentage (0-100) |
| `http_client_cache_size` | Gauge | Current number of cached entries |
| `http_client_cache_evictions_total` | Counter | Total number of LRU evictions |
| `http_client_cache_expirations_total` | Counter | Total number of TTL expirations |

### Retry Metrics (Issue #1453)

The retry system provides the following Prometheus metrics:

| Metric Name | Type | Description |
|-------------|------|-------------|
| `http_client_retries_total` | Counter | Total number of retry attempts |
| `http_client_success_after_retry_total` | Counter | Successful requests after retry |
| `http_client_failed_after_retry_total` | Counter | Failed requests after all retries |
| `http_client_retries_by_status{status_code}` | Counter | Retry attempts by HTTP status code |
| `http_client_retries_by_endpoint{endpoint}` | Counter | Retry attempts by endpoint |
| `http_client_retry_delay_avg_ms` | Gauge | Average retry delay in milliseconds |
| `http_client_retry_delay_total_ms` | Counter | Total retry delay in milliseconds |

## Usage

### Exporting Metrics

```typescript
import { exportCacheMetricsPrometheus } from '@/lib/api/core/requestCache';
import { exportPrometheusMetrics } from '@/lib/api/core/metrics';

// Export cache metrics
const cacheMetrics = exportCacheMetricsPrometheus();

// Export retry metrics
const retryMetrics = exportPrometheusMetrics();

// Combine all metrics
const allMetrics = cacheMetrics + retryMetrics;
```

### Next.js API Route Example

Create a metrics endpoint at `app/api/metrics/route.ts`:

```typescript
import { NextResponse } from 'next/server';
import { exportCacheMetricsPrometheus } from '@/lib/api/core/requestCache';
import { exportPrometheusMetrics } from '@/lib/api/core/metrics';

export async function GET() {
  // Combine all metrics
  const metrics = exportCacheMetricsPrometheus() + exportPrometheusMetrics();

  return new NextResponse(metrics, {
    status: 200,
    headers: {
      'Content-Type': 'text/plain; version=0.0.4; charset=utf-8',
    },
  });
}
```

### Prometheus Configuration

Add the following to your `prometheus.yml`:

```yaml
scrape_configs:
  - job_name: 'meepleai-web'
    scrape_interval: 15s
    static_configs:
      - targets: ['localhost:3000']
    metrics_path: '/api/metrics'
```

## Example Output

```
# HELP http_client_cache_hits_total Total number of cache hits
# TYPE http_client_cache_hits_total counter
http_client_cache_hits_total 142

# HELP http_client_cache_misses_total Total number of cache misses
# TYPE http_client_cache_misses_total counter
http_client_cache_misses_total 58

# HELP http_client_cache_hit_rate_percent Cache hit rate percentage
# TYPE http_client_cache_hit_rate_percent gauge
http_client_cache_hit_rate_percent 71.00

# HELP http_client_cache_size Current number of cached entries
# TYPE http_client_cache_size gauge
http_client_cache_size 12

# HELP http_client_cache_evictions_total Total number of cache evictions (LRU)
# TYPE http_client_cache_evictions_total counter
http_client_cache_evictions_total 3

# HELP http_client_cache_expirations_total Total number of cache expirations (TTL)
# TYPE http_client_cache_expirations_total counter
http_client_cache_expirations_total 45
```

## Grafana Dashboard

### Key Panels

1. **Cache Hit Rate** (Gauge):
   ```promql
   http_client_cache_hit_rate_percent
   ```

2. **Cache Hits vs Misses** (Graph):
   ```promql
   rate(http_client_cache_hits_total[5m])
   rate(http_client_cache_misses_total[5m])
   ```

3. **Cache Size** (Graph):
   ```promql
   http_client_cache_size
   ```

4. **Evictions and Expirations** (Graph):
   ```promql
   rate(http_client_cache_evictions_total[5m])
   rate(http_client_cache_expirations_total[5m])
   ```

5. **Retry Success Rate** (Graph):
   ```promql
   rate(http_client_success_after_retry_total[5m]) / rate(http_client_retries_total[5m]) * 100
   ```

## Alerts

### Recommended Prometheus Alerts

```yaml
groups:
  - name: http_client_cache
    rules:
      # Low cache hit rate
      - alert: LowCacheHitRate
        expr: http_client_cache_hit_rate_percent < 50
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Low HTTP client cache hit rate"
          description: "Cache hit rate is {{ $value }}% (threshold: 50%)"

      # High eviction rate
      - alert: HighCacheEvictionRate
        expr: rate(http_client_cache_evictions_total[5m]) > 10
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High cache eviction rate"
          description: "Cache eviction rate is {{ $value }}/s (threshold: 10/s)"

      # High retry failure rate
      - alert: HighRetryFailureRate
        expr: rate(http_client_failed_after_retry_total[5m]) > 1
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "High HTTP retry failure rate"
          description: "Retry failure rate is {{ $value }}/s (threshold: 1/s)"
```

## Security Considerations

1. **Authentication**: Protect the `/api/metrics` endpoint with authentication in production
2. **Rate Limiting**: Implement rate limiting to prevent metric scraping abuse
3. **Network**: Restrict access to Prometheus server IPs only

Example with middleware:

```typescript
import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // Protect metrics endpoint
  if (pathname === '/api/metrics') {
    const ip = request.ip || request.headers.get('x-forwarded-for');
    const allowedIPs = process.env.PROMETHEUS_ALLOWED_IPS?.split(',') || [];

    if (!allowedIPs.includes(ip || '')) {
      return new NextResponse('Forbidden', { status: 403 });
    }
  }

  return NextResponse.next();
}
```

## References

- [Prometheus Exposition Formats](https://prometheus.io/docs/instrumenting/exposition_formats/)
- [Prometheus Best Practices](https://prometheus.io/docs/practices/naming/)
- [Grafana Prometheus Data Source](https://grafana.com/docs/grafana/latest/datasources/prometheus/)
