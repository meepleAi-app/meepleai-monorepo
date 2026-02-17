# Load Testing Performance Baselines

**Issue #2928** | **Last Updated**: 2026-02-01

## Overview

This document establishes performance baselines for MeepleAI API endpoints under load testing scenarios. These baselines serve as targets for performance validation and regression detection.

## Baseline Targets

### Response Time Targets

| Category | p50 | p90 | p95 | p99 | Max |
|----------|-----|-----|-----|-----|-----|
| **User Reads** | <100ms | <300ms | <500ms | <1000ms | <2000ms |
| **User Writes** | <150ms | <400ms | <600ms | <1200ms | <2500ms |
| **Admin Reads** | <150ms | <400ms | <500ms | <1000ms | <2000ms |
| **Admin Writes** | <200ms | <500ms | <750ms | <1500ms | <3000ms |
| **Search Operations** | <200ms | <400ms | <500ms | <1000ms | <2000ms |
| **Batch Operations** | <300ms | <700ms | <1000ms | <2000ms | <5000ms |

### Throughput Targets

| Scenario | Min Throughput | Target | Peak Capacity |
|----------|---------------|--------|---------------|
| User Dashboard | 10 req/s | 50 req/s | 100 req/s |
| Library Browsing | 20 req/s | 100 req/s | 200 req/s |
| Catalog Search | 30 req/s | 150 req/s | 300 req/s |
| Admin Operations | 5 req/s | 25 req/s | 50 req/s |

### Error Rate Targets

| Scenario | Target | Warning | Critical |
|----------|--------|---------|----------|
| All Read Operations | <0.1% | >0.5% | >1% |
| All Write Operations | <0.5% | >1% | >2% |
| Search Operations | <0.1% | >0.5% | >1% |
| Concurrent Writes | <1% | >2% | >5% |

## Scenario-Specific Baselines

### 1. User Dashboard Polling

**Test Configuration**: 50 VUs, 5 minutes duration

| Endpoint | p95 Target | p99 Target | Error Rate |
|----------|------------|------------|------------|
| `/api/v1/users/profile` | <300ms | <600ms | <0.1% |
| `/api/v1/notifications` | <400ms | <800ms | <0.1% |
| `/api/v1/users/me/activity` | <350ms | <700ms | <0.1% |
| `/api/v1/users/me/upload-quota` | <200ms | <400ms | <0.1% |
| `/api/v1/users/me/ai-usage` | <300ms | <600ms | <0.1% |

**Aggregate Metrics**:
```
dashboard_polling_latency: p(95)<500ms, p(99)<1000ms
notification_latency: p(95)<500ms, p(99)<1000ms
profile_latency: p(95)<400ms, p(99)<800ms
dashboard_errors: count<10 per 5min
notification_errors: count<20 per 5min
```

### 2. Library Browsing

**Test Configuration**: 50 VUs, 5 minutes duration

| Endpoint | p95 Target | p99 Target | Error Rate |
|----------|------------|------------|------------|
| `/api/v1/library` (list) | <400ms | <800ms | <0.1% |
| `/api/v1/library/stats` | <200ms | <400ms | <0.1% |
| `/api/v1/library/quota` | <150ms | <300ms | <0.1% |
| `/api/v1/library/games/{id}` | <500ms | <1000ms | <0.1% |
| `/api/v1/library/games/{id}/status` | <300ms | <600ms | <0.1% |

**Aggregate Metrics**:
```
library_browsing_latency: p(95)<500ms, p(99)<1000ms
pagination_latency: p(95)<500ms, p(99)<1000ms
detail_view_latency: p(95)<750ms, p(99)<1500ms
library_errors: count<10 per 5min
pagination_errors: count<20 per 5min
```

### 3. Catalog Search

**Test Configuration**: 75 VUs, 5 minutes duration

| Endpoint | p95 Target | p99 Target | Error Rate |
|----------|------------|------------|------------|
| `/api/v1/shared-games` (search) | <400ms | <800ms | <0.1% |
| `/api/v1/shared-games` (filtered) | <450ms | <900ms | <0.1% |
| `/api/v1/shared-games/{id}` | <500ms | <1000ms | <0.1% |
| `/api/v1/shared-games/stats` | <200ms | <400ms | <0.1% |

**Aggregate Metrics**:
```
catalog_search_latency: p(95)<500ms, p(99)<1000ms
catalog_filter_latency: p(95)<500ms, p(99)<1000ms
catalog_detail_latency: p(95)<750ms, p(99)<1500ms
catalog_errors: count<20 per 5min
search_errors: count<30 per 5min
```

### 4. Admin Concurrent Actions

**Test Configuration**: 20 VUs, 5 minutes duration

| Operation Type | p95 Target | p99 Target | Error Rate |
|----------------|------------|------------|------------|
| Read Operations | <500ms | <1000ms | <0.5% |
| Write Operations | <750ms | <1500ms | <1% |
| Batch Operations | <1000ms | <2000ms | <1% |

**Specific Endpoints**:

| Endpoint | p95 Target | p99 Target |
|----------|------------|------------|
| `/api/v1/admin/llm/efficiency-report` | <500ms | <1000ms |
| `/api/v1/admin/reports/system-health` | <400ms | <800ms |
| `/api/v1/admin/users` (paginated) | <500ms | <1000ms |
| `/api/v1/admin/audit-log` | <600ms | <1200ms |
| `/api/v1/admin/alert-configuration` (PUT) | <600ms | <1200ms |
| `/api/v1/admin/cache/invalidate` (POST) | <400ms | <800ms |

**Aggregate Metrics**:
```
admin_read_latency: p(95)<500ms, p(99)<1000ms
admin_write_latency: p(95)<750ms, p(99)<1500ms
batch_operation_latency: p(95)<1000ms, p(99)<2000ms
admin_read_errors: count<10 per 5min
admin_write_errors: count<5 per 5min
concurrency_errors: count<3 per 5min
```

### 5. Admin Polling

**Test Configuration**: 10 VUs, 5 minutes duration

| Endpoint | p95 Target | p99 Target | Error Rate |
|----------|------------|------------|------------|
| `/api/v1/admin/llm/efficiency-report` | <500ms | <1000ms | <0.1% |
| `/api/v1/admin/llm/monthly-report` | <600ms | <1200ms | <0.1% |
| `/api/v1/admin/reports` | <400ms | <800ms | <0.1% |
| `/api/v1/admin/ai-models` | <300ms | <600ms | <0.1% |
| `/api/v1/admin/tier-routing` | <250ms | <500ms | <0.1% |

## Load Profiles

### Smoke Test Profile

**Purpose**: Quick validation that tests work correctly

```javascript
executor: 'constant-vus',
vus: 3-5,
duration: '1m'
```

**Expectations**:
- All endpoints respond successfully
- No errors
- Response times within baseline
- Authentication works

### Load Test Profile

**Purpose**: Normal production load simulation

```javascript
executor: 'constant-vus',
vus: 20-75 (varies by scenario),
duration: '5m'
```

**Expectations**:
- p95 response times meet baselines
- Error rate < 1%
- Throughput meets targets
- No degradation over time

### Stress Test Profile

**Purpose**: Find breaking points and recovery behavior

```javascript
executor: 'ramping-vus',
stages: [
  { duration: '2m', target: start_vus },
  { duration: '3m', target: peak_vus },
  { duration: '3m', target: sustained_vus },
  { duration: '2m', target: 0 },
]
```

**Expectations**:
- System handles peak load
- Graceful degradation (not crash)
- Recovery after load reduction
- Error rate < 5% at peak

### Soak Test Profile (Extended)

**Purpose**: Memory leaks, connection pool exhaustion, long-running stability

```javascript
executor: 'constant-vus',
vus: 25,
duration: '30m-2h'
```

**Expectations**:
- No performance degradation over time
- Memory usage stable
- Connection pools healthy
- No cumulative errors

## Capacity Planning

### Current Baseline Capacity

| Resource | Current Limit | Safe Operating Limit | Peak Limit |
|----------|--------------|---------------------|------------|
| Concurrent Users | 500 | 400 | 750 |
| Requests/second | 500 | 400 | 800 |
| Database Connections | 100 | 80 | 150 |
| Memory (API) | 2GB | 1.5GB | 3GB |
| CPU (API) | 4 cores | 3 cores | 6 cores |

### Scaling Triggers

| Metric | Warning Threshold | Action Required |
|--------|-------------------|-----------------|
| p95 Response Time | >500ms sustained | Scale horizontally |
| Error Rate | >1% sustained | Investigate + scale |
| CPU Usage | >70% sustained | Scale vertically |
| Memory Usage | >80% | Investigate leaks |
| Connection Pool | >80% | Increase pool size |

## Monitoring Integration

### Prometheus Metrics

```yaml
# Key metrics to monitor
- http_req_duration_seconds{quantile="0.95"}
- http_req_duration_seconds{quantile="0.99"}
- http_requests_total
- http_request_errors_total
- k6_vus
- k6_iterations_total
```

### Grafana Alerts

```yaml
# Alert thresholds
p95_response_warning: >500ms for 5m
p95_response_critical: >1000ms for 2m
error_rate_warning: >0.5% for 5m
error_rate_critical: >1% for 2m
throughput_low: <10 req/s for 5m
```

## Baseline Validation Process

### Initial Baseline Establishment

1. Run smoke tests to verify setup
2. Run load tests 3 times in succession
3. Calculate average metrics
4. Set baselines at p95 + 20% margin
5. Document environmental conditions

### Ongoing Validation

1. Run load tests on schedule (weekly)
2. Compare against established baselines
3. Flag regressions >20% degradation
4. Update baselines after major releases
5. Track trends over time

### Regression Detection

```javascript
// Example threshold with baseline comparison
thresholds: {
  'http_req_duration': [
    'p(95)<500',  // Absolute threshold
    'p(95)<baseline*1.2',  // 20% regression margin
  ],
}
```

## Environmental Factors

### Test Environment Specifications

| Component | Development | Staging | Production |
|-----------|-------------|---------|------------|
| API Instances | 1 | 2 | 4+ |
| Database | Single | Primary+Replica | Cluster |
| Redis | Single | Single | Cluster |
| Network | Local | Cloud | Cloud+CDN |

### Baseline Adjustment Factors

| Environment | Adjustment |
|-------------|------------|
| Local Docker | +50% tolerance |
| CI/CD Runner | +30% tolerance |
| Staging | +10% tolerance |
| Production | Baseline (1x) |

## Historical Baselines

### Version History

| Version | Date | Change | Impact |
|---------|------|--------|--------|
| v1.0 | 2026-02-01 | Initial baselines | Baseline established |

### Baseline Updates

Document any baseline changes with:
- Reason for change
- Before/after metrics
- Environmental changes
- Code changes affecting performance

## See Also

- [Load Testing Guide](./load-testing-guide.md) - How to run tests
- [Performance Benchmarks](./performance-benchmarks.md) - Test suite performance
- [Grafana Dashboards](../../infra/monitoring/grafana/dashboards/) - Monitoring
