# OPS-05: Error Monitoring & Alerting - Technical Design

**Status**: Implemented
**Issue**: #295
**Dependencies**: OPS-01 (Structured Logging), OPS-02 (OpenTelemetry)
**Implementation Date**: 2025-10-16

## Overview

OPS-05 implements comprehensive error monitoring and alerting for the MeepleAI platform using a Grafana-native stack. This solution extends the existing observability infrastructure (Prometheus, Grafana, Seq, Jaeger) with automated alerting, error aggregation, and real-time dashboards.

## Architecture

### Solution: Grafana-Native Stack

We chose **Solution 1** (Grafana-Native Stack) over Sentry because:
- **Zero incremental cost** - Uses existing open-source tools
- **Deep integration** - Extends OPS-01 and OPS-02 infrastructure
- **Operational simplicity** - +2 Docker services vs +7 for Sentry
- **Team familiarity** - Prometheus/Grafana already known
- **Single pane of glass** - All observability in Grafana

```
┌─────────────────────────────────────────────────────────┐
│                    MeepleAI API                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │   Enhanced Error Metrics + Middleware              │ │
│  │   - meepleai.api.errors.total (Counter)            │ │
│  │   - meepleai.api.errors.unhandled (Counter)        │ │
│  │   - Tags: endpoint, status_code, exception_type,   │ │
│  │           severity, error_category                 │ │
│  └────────────────────┬───────────────────────────────┘ │
└─────────────────────────┼───────────────────────────────┘
                          │
                ┌─────────┴─────────┐
                │                   │
                ▼                   ▼
        ┌──────────────┐    ┌──────────────┐
        │  Prometheus  │    │     Seq      │
        │   /metrics   │    │   (logs)     │
        │  + Rules     │    │              │
        └──────┬───────┘    └──────────────┘
               │
               │ Alert Rules (PromQL)
               │ - error_rate > threshold
               │ - error_spike detection
               │ - dependency health
               │
               ▼
        ┌──────────────┐
        │ Alertmanager │
        │ - Grouping   │
        │ - Throttling │
        │ - Routing    │
        └──────┬───────┘
               │
               ├──────────┬──────────┐
               ▼          ▼          ▼
        ┌──────────┐ ┌──────┐  ┌────────┐
        │ Grafana  │ │ Slack│  │ Email  │
        │ OnCall   │ │      │  │        │
        └──────────┘ └──────┘  └────────┘
               │
               ▼
        ┌──────────────┐
        │   Grafana    │
        │ Error Dash   │
        │ (11 panels)  │
        └──────────────┘
```

## Components

### 1. Prometheus Alertmanager

**Purpose**: Alert routing, deduplication, and multi-channel notifications

**Configuration** (`infra/alertmanager.yml`):
- **Grouping**: By `alertname`, `service`, `severity`
- **Group Wait**: 10s (collect similar alerts before sending)
- **Repeat Interval**: 4h (unresolved alerts)
- **Receivers**: Grafana OnCall (webhook), Slack, Email

**Key Features**:
- Automatic deduplication of similar alerts
- Severity-based routing (critical: 0s wait, warning: 30s wait)
- Inhibition rules (suppress warnings when critical fires)

### 2. Grafana OnCall

**Purpose**: On-call management, escalation policies, incident response

**Configuration** (`docker-compose.yml`):
- Port: 8082
- Database: SQLite3 (lightweight)
- Broker: Redis (celery workers)

**Key Features**:
- Webhook integration with Alertmanager
- On-call scheduling (24/7 rotation)
- Escalation policies (primary → secondary → manager)
- Multi-channel notifications (Slack, Email, SMS)

### 3. Prometheus Recording & Alert Rules

**Recording Rules** (`infra/prometheus-rules.yml`):
Pre-computed metrics for fast querying:
- `meepleai:api:error_rate:5m` - Errors per second
- `meepleai:api:error_ratio:5m` - Percentage of requests failing
- `meepleai:rag:error_rate:5m` - RAG-specific errors
- `meepleai:api:error_spike_ratio` - Anomaly detection (3x baseline)

**Alert Rules** (12 total):

| Alert Name | Severity | Threshold | Duration |
|------------|----------|-----------|----------|
| HighErrorRate | Critical | > 1 error/sec | 2m |
| ErrorSpike | Critical | 3x baseline | 2m |
| DatabaseDown | Critical | DB unreachable | 1m |
| RedisDown | Critical | Redis unreachable | 2m |
| QdrantDown | Critical | Qdrant unreachable | 2m |
| HighErrorRatio | Warning | > 5% | 5m |
| RagErrorsDetected | Warning | > 0.5 error/sec | 3m |
| SlowResponseTime | Warning | p95 > 5s | 5m |
| HighMemoryUsage | Warning | > 80% | 5m |
| NewErrorTypeDetected | Info | New error pattern | 5m |

### 4. API Error Metrics

**New Metrics** (`apps/api/src/Api/Observability/MeepleAiMetrics.cs`):

```csharp
// Counter: Total API errors
public static Counter<long> ApiErrorsTotal { get; } = Meter.CreateCounter<long>(
    name: "meepleai.api.errors.total",
    unit: "errors",
    description: "Total number of API errors");

// Counter: Unhandled exceptions
public static Counter<long> UnhandledErrorsTotal { get; } = Meter.CreateCounter<long>(
    name: "meepleai.api.errors.unhandled",
    unit: "errors",
    description: "Total number of unhandled exceptions");
```

**Metric Labels**:
- `http.status_code` - HTTP status (400, 500, etc.)
- `exception.type` - Exception class (TimeoutException, etc.)
- `severity` - critical (5xx), warning (4xx), info
- `error.category` - validation, system, dependency, timeout, authorization, notfound, unknown
- `http.route` - Endpoint route template (e.g., `/api/v1/games/{id}`)

**Exception Classification**:
- **Validation**: ArgumentException, ArgumentNullException, ArgumentOutOfRangeException
- **System**: OutOfMemoryException, StackOverflowException, AccessViolationException, DivideByZeroException
- **Dependency**: HttpRequestException, TimeoutException, SocketException, SqlException
- **Timeout**: TaskCanceledException, OperationCanceledException, TimeoutException
- **Authorization**: UnauthorizedAccessException, SecurityException
- **NotFound**: FileNotFoundException, DirectoryNotFoundException, KeyNotFoundException
- **Unknown**: All other exceptions

**Middleware Integration** (`apps/api/src/Api/Middleware/ApiExceptionHandlerMiddleware.cs`):
```csharp
MeepleAiMetrics.RecordApiError(
    httpContext: context,
    exception: exception,
    statusCode: statusCode);
```sql
### 5. Grafana Dashboard

**Dashboard** (`infra/dashboards/error-monitoring.json`):
- **Title**: "MeepleAI - Error Monitoring & Alerting"
- **UID**: `meepleai-error-monitoring`
- **Refresh**: 5s
- **Panels**: 11 total

**Panel Layout**:

**Row 1: Error Overview**
1. **Error Rate** (Time series) - 5xx errors, total API errors, unhandled errors
2. **Error Ratio** (Gauge) - % of requests failing (thresholds: 1%, 5%, 10%)
3. **Active Alerts** (Stat) - Count of firing alerts

**Row 2: Error Distribution**
4. **Top 10 Endpoints** (Bar chart time series) - Error rate by endpoint
5. **Status Code Distribution** (Pie chart) - Errors by HTTP status code
6. **Error Trend by Category** (Stacked time series) - validation, system, dependency, etc.

**Row 3: AI/RAG Errors**
7. **RAG Error Rate** (Time series) - RAG-specific errors
8. **Exception Type Distribution** (Donut chart) - Errors by exception class

**Row 4: Dependency Health**
9. **PostgreSQL Status** (Stat) - up/down indicator
10. **Redis Status** (Stat) - up/down indicator
11. **Qdrant Status** (Stat) - up/down indicator

### 6. Correlation Across Systems

**X-Correlation-Id** (from OPS-01) ties together:
- **Prometheus metrics** - Tagged with `correlation_id` label
- **Seq logs** - Searchable by `RequestId` property
- **Jaeger traces** - Trace ID = Correlation ID
- **Alert notifications** - Include correlation ID in payload

**Example Workflow**:
1. Alert fires: "HighErrorRate" for `/api/v1/games`
2. Alert includes correlation ID: `0HN6G8QJ9KL0M:00000001`
3. Engineer clicks link to Seq: `http://localhost:8081/#/events?filter=RequestId%3D%220HN6G8QJ9KL0M%3A00000001%22`
4. Engineer views full logs with request/response/exception details
5. Engineer clicks link to Jaeger: `http://localhost:16686/trace/0HN6G8QJ9KL0M:00000001`
6. Engineer sees distributed trace showing slowdown in Qdrant query

## Configuration

### Environment Variables

Add to `infra/env/api.env.dev` (optional):
```bash
# Slack webhook for Alertmanager notifications
SLACK_WEBHOOK_URL=https://hooks.slack.com/services/YOUR/WEBHOOK/URL

# Grafana OnCall secret key (generate secure random string)
ONCALL_SECRET_KEY=your-secure-secret-key-here
ONCALL_PROMETHEUS_SECRET=your-prometheus-secret-here
```

### Docker Services

**New Services** (`docker-compose.yml`):
- `alertmanager` - Port 9093
- `grafana-oncall` - Port 8082

**New Volumes**:
- `alertmanagerdata`
- `oncalldata`

### Prometheus Integration

**Updated** (`infra/prometheus.yml`):
```yaml
# Load alert rules
rule_files:
  - '/etc/prometheus/prometheus-rules.yml'

# Send alerts to Alertmanager
alerting:
  alertmanagers:
    - static_configs:
        - targets: ['alertmanager:9093']
```

## Alerting Strategy

### Severity Levels

| Severity | Use Case | Response Time | Notification |
|----------|----------|---------------|--------------|
| **Critical** | Service down, high error rate, all deps down | Immediate | PagerDuty + Slack + Email |
| **Warning** | Elevated errors, single dep down, slow response | 2-5 minutes | Slack + Email |
| **Info** | New error type, threshold warnings | 5+ minutes | Slack only |

### Alert Deduplication

**Prometheus-Level**:
- Recording rules aggregate by `endpoint`, `status_code`, `service`
- Reduces cardinality for fast queries

**Alertmanager-Level**:
- Groups by `alertname`, `service`, `severity`
- Waits 10s for similar alerts before sending
- Re-sends every 5m if new alerts arrive

**Grafana OnCall-Level**:
- Fingerprinting based on alert labels
- Auto-resolves when Alertmanager sends "resolved" status

### Alert Routing

```yaml
# Critical alerts
- severity: critical
  → grafana-oncall (0s wait, 15m repeat)
  → Slack #alerts
  → Email oncall@meepleai.dev

# Warning alerts
- severity: warning
  → grafana-oncall (30s wait, 2h repeat)
  → Slack #monitoring

# Info alerts
- severity: info
  → grafana-oncall (5m wait, 4h repeat)
  → Slack #engineering
```json
## Testing

### Manual Testing

1. **Trigger Test Error**:
   ```bash
   curl -X POST http://localhost:8080/api/v1/test-error
   ```

2. **Verify Metrics**:
   ```bash
   curl http://localhost:8080/metrics | grep meepleai_api_errors
   ```

3. **Check Prometheus Alert**:
   - Open http://localhost:9090/alerts
   - Verify `HighErrorRate` fires after 2 minutes

4. **Check Alertmanager**:
   - Open http://localhost:9093
   - Verify alert appears in UI

5. **Check Grafana Dashboard**:
   - Open http://localhost:3001/d/meepleai-error-monitoring
   - Verify error rate increases

### Unit Tests

See `apps/api/tests/Api.Tests/Observability/ErrorMetricsTests.cs`:
- `RecordApiError_IncrementsCounters()`
- `ClassifyException_CorrectlyCategorizesExceptions()`
- `RecordApiError_ExtractsRoutePattern()`

### Integration Tests

See `apps/api/tests/Api.Tests/Integration/ErrorMonitoringIntegrationTests.cs`:
- `UnhandledException_RecordsMetrics()`
- `ErrorMetrics_ExportedInPrometheusFormat()`
- `HighErrorRate_TriggersAlert()`

## Operational Procedures

### Viewing Alerts

**Prometheus UI**:
```
http://localhost:9090/alerts
```

**Alertmanager UI**:
```
http://localhost:9093
```

**Grafana OnCall UI**:
```
http://localhost:8082
```

### Silencing Alerts

**Planned Maintenance**:
1. Open Alertmanager UI: http://localhost:9093
2. Click "Silences" → "New Silence"
3. Set matcher: `alertname=HighErrorRate`
4. Set duration: 1 hour
5. Add comment: "Deploying v1.2.0"

**Alternative (Grafana OnCall)**:
1. Open OnCall UI
2. Find alert
3. Click "Snooze" → Select duration

### Investigating Errors

**Step 1: Check Dashboard**
- Open http://localhost:3001/d/meepleai-error-monitoring
- Identify error spike time and affected endpoints

**Step 2: Check Logs (Seq)**
- Open http://localhost:8081
- Filter by time range: `@Timestamp > '2025-10-16T10:00:00Z'`
- Filter by endpoint: `RequestPath = '/api/v1/games'`
- Look for error logs (`@Level = 'Error'`)

**Step 3: Check Traces (Jaeger)**
- Open http://localhost:16686
- Search by service: `meepleai-api`
- Filter by status: `error`
- Identify slow operations

**Step 4: Correlate with Correlation ID**
- Find correlation ID in alert payload
- Search Seq: `RequestId = "0HN6G8QJ9KL0M:00000001"`
- Search Jaeger: paste correlation ID in trace search

## Performance Impact

### Metrics Collection

- **Overhead**: < 1% CPU, ~10 MB RAM
- **Storage**: ~50 MB per day (30-day retention = 1.5 GB)
- **Network**: ~1 KB per metric scrape (15s interval)

### Alert Evaluation

- **Prometheus**: Evaluates rules every 15s
- **Alertmanager**: Processes alerts in < 10ms
- **Grafana OnCall**: Webhook processing < 50ms

### Dashboard Queries

- **Recording Rules**: Pre-computed, query time < 10ms
- **Raw Metrics**: Query time 50-200ms (depends on time range)

## Cost Analysis

### Infrastructure

- **Alertmanager**: 0 MB disk, 50 MB RAM
- **Grafana OnCall**: ~500 MB disk (SQLite), 200 MB RAM
- **Prometheus**: +50 MB per day for error metrics

**Total**: ~$0/month (open-source only)

### Development Time

- **Initial Implementation**: 24 hours (3 days)
- **Testing & Documentation**: 12 hours
- **Team Training**: 4 hours

**Total**: ~40 hours (~5 days)

### Ongoing Maintenance

- **Alert tuning**: 2 hours/month (first 3 months)
- **Dashboard updates**: 1 hour/quarter
- **Prometheus upgrades**: 2 hours/quarter

**Total**: ~12 hours/year after stabilization

## Future Enhancements

### Short Term (Next 3 Months)

1. **Slack Integration**: Configure Slack webhook in `alertmanager.yml`
2. **PagerDuty Integration**: Add PagerDuty receiver for critical alerts
3. **Alert Tuning**: Adjust thresholds based on actual traffic patterns
4. **Runbook URLs**: Add detailed runbooks for each alert type

### Medium Term (Next 6 Months)

1. **Anomaly Detection**: ML-based error spike detection (beyond 3x baseline)
2. **User Impact Tracking**: Count unique users affected by errors
3. **Error Grouping**: Automatically group similar errors in Grafana
4. **Mobile App**: Grafana OnCall mobile app for on-call engineers

### Long Term (Next Year)

1. **Sentry Integration**: Add Sentry for detailed error context if volume grows > 10k/month
2. **Long-Term Storage**: Thanos or VictoriaMetrics for > 30-day metric retention
3. **Synthetic Monitoring**: Proactive health checks simulating user behavior
4. **Auto-Remediation**: Automated responses to common errors (restart service, scale up, etc.)

## Troubleshooting

### Problem: Alerts Not Firing

**Symptoms**: Expected alert doesn't appear in Alertmanager

**Diagnosis**:
1. Check Prometheus rule evaluation: http://localhost:9090/rules
2. Check rule state: "Pending" (evaluating) vs "Firing" (alerting)
3. Check `for` duration: Rule may be waiting for sustained condition

**Resolution**:
- If rule is "Pending", wait for `for` duration to expire
- If rule never fires, check PromQL query returns > 0
- Test query in Prometheus UI: http://localhost:9090/graph

### Problem: Too Many Alerts (Alert Fatigue)

**Symptoms**: Constant notifications, team ignores alerts

**Diagnosis**:
1. Check alert frequency in Alertmanager UI
2. Identify most frequent alerts

**Resolution**:
- Increase thresholds (e.g., `error_rate > 1` → `error_rate > 2`)
- Increase `for` duration (e.g., `for: 2m` → `for: 5m`)
- Add inhibition rules to suppress related alerts
- Use `group_wait` and `group_interval` to batch notifications

### Problem: Metrics Not Appearing in Prometheus

**Symptoms**: Dashboard shows "No data"

**Diagnosis**:
1. Check API metrics endpoint: `curl http://localhost:8080/metrics`
2. Check Prometheus targets: http://localhost:9090/targets
3. Check Prometheus logs: `docker compose logs prometheus`

**Resolution**:
- Verify API is exposing metrics at `/metrics`
- Verify Prometheus can reach API (network connectivity)
- Check Prometheus scrape config includes `job_name: 'meepleai-api'`

### Problem: Grafana Dashboard Empty

**Symptoms**: Dashboard loads but panels show no data

**Diagnosis**:
1. Check Prometheus datasource in Grafana: http://localhost:3001/datasources
2. Test datasource connection: Should show "Data source is working"
3. Check panel queries: Click "Edit" on panel, check PromQL syntax

**Resolution**:
- Verify Prometheus datasource URL: `http://prometheus:9090`
- Re-import dashboard JSON from `infra/dashboards/error-monitoring.json`
- Check Prometheus has data: http://localhost:9090/graph

## Security Considerations

### Sensitive Data in Alerts

**Risk**: Error messages may contain sensitive data (API keys, passwords, PII)

**Mitigation**:
- Middleware redacts sensitive data before recording metrics
- Alertmanager payload excludes full exception messages
- Seq logs redact sensitive properties (configured in OPS-04)

### Alert Endpoint Security

**Risk**: Alertmanager webhook endpoint exposed without authentication

**Mitigation**:
- Grafana OnCall webhook uses HTTPS in production
- Alertmanager uses mTLS for webhook requests (configurable)
- Network policies restrict Alertmanager to internal traffic only

### Access Control

**Risk**: Unauthorized access to error dashboards reveals system internals

**Mitigation**:
- Grafana authentication required (admin/admin in dev, SSO in prod)
- Alertmanager UI protected by basic auth or OAuth
- Prometheus UI behind reverse proxy with authentication

## References

- **Issue #295**: https://github.com/DegrassiAaron/meepleai-monorepo/issues/295
- **OPS-01 (Structured Logging)**: `docs/observability.md`
- **OPS-02 (OpenTelemetry)**: `docs/ops-02-opentelemetry-design.md`
- **Prometheus Alerting**: https://prometheus.io/docs/alerting/latest/overview/
- **Grafana OnCall**: https://grafana.com/docs/oncall/latest/
- **Alertmanager Config**: https://prometheus.io/docs/alerting/latest/configuration/

## Changelog

- **2025-10-16**: Initial implementation (v1.0)
  - Added Alertmanager + Grafana OnCall to docker-compose.yml
  - Created prometheus-rules.yml with 12 alert rules
  - Added error-specific metrics to API (MeepleAiMetrics.cs)
  - Created Grafana error monitoring dashboard (11 panels)
  - Updated middleware to record error metrics
  - Integrated Prometheus with Alertmanager
