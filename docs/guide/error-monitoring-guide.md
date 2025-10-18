# Error Monitoring & Alerting - User Guide

Quick guide for using the MeepleAI error monitoring and alerting system (OPS-05).

## Quick Start

### View Error Dashboard

1. Open Grafana: http://localhost:3001
2. Login: `admin` / `admin` (dev environment)
3. Navigate to **Dashboards** → **MeepleAI - Error Monitoring & Alerting**
4. Dashboard auto-refreshes every 5 seconds

### View Active Alerts

**Prometheus Alerts**:
```json
http://localhost:9090/alerts
```

**Alertmanager**:
```json
http://localhost:9093
```

**Grafana OnCall**:
```json
http://localhost:8082
```

## Dashboard Overview

### Error Overview (Top Row)

**Error Rate** - Shows errors per second over time
- **5xx Errors**: Server errors (500, 502, 503, 504)
- **Total API Errors**: All errors tracked by custom metrics
- **Unhandled Errors**: Exceptions that reached the middleware

**Thresholds**:
- 🟢 Green: < 0.5 errors/sec (healthy)
- 🟡 Yellow: 0.5-1 errors/sec (warning)
- 🔴 Red: > 1 error/sec (critical)

**Error Ratio** - Percentage of requests failing
- Shows what % of all requests result in errors
- 🟢 < 1%: Excellent
- 🟡 1-5%: Acceptable
- 🟠 5-10%: Degraded
- 🔴 > 10%: Critical

**Active Alerts** - Count of firing alerts
- Shows how many alerts are currently active
- Click to jump to Alertmanager UI
- 🟢 0: All good
- 🟡 1-2: Monitor
- 🔴 3+: Investigate

### Error Distribution (Middle Rows)

**Top 10 Endpoints by Error Rate**
- Bar chart showing which endpoints have most errors
- Helps identify problematic routes
- Example: `/api/v1/games/{id}` may show high error rate

**Error Distribution by Status Code**
- Pie chart: 500, 502, 503, 504
- Helps understand error types (server vs gateway vs timeout)

**Error Trend by Category**
- Stacked area chart showing error categories over time:
  - **validation**: Bad input (ArgumentException)
  - **system**: Server issues (OutOfMemory)
  - **dependency**: External service failures (Postgres, Redis, Qdrant)
  - **timeout**: Slow operations
  - **authorization**: Permission issues
  - **notfound**: Missing resources
  - **unknown**: Other exceptions

**RAG Error Rate**
- Specific to RAG/AI operations
- Tracks errors in semantic search and Q&A

**Error Distribution by Exception Type**
- Donut chart showing which exception classes occur most
- Example: `TimeoutException`, `HttpRequestException`, `SqlException`

### Dependency Health (Bottom Row)

Three status indicators:
- **PostgreSQL**: 🟢 Up / 🔴 Down
- **Redis**: 🟢 Up / 🔴 Down
- **Qdrant**: 🟢 Up / 🔴 Down

If any show red, check `docker compose ps` to see if container is running.

## Alert Types

### Critical Alerts (Immediate Response)

**HighErrorRate**: > 1 error/sec for 2 minutes
- **What it means**: Multiple errors happening quickly
- **Action**: Check logs, recent deployments, dependency health

**ErrorSpike**: Error rate 3x higher than normal
- **What it means**: Sudden increase in errors
- **Action**: Check recent changes, rollback if needed

**DatabaseDown / RedisDown / QdrantDown**: Dependency unreachable
- **What it means**: Core service is unavailable
- **Action**: Check `docker compose ps`, restart if needed

### Warning Alerts (Monitor & Investigate)

**HighErrorRatio**: > 5% of requests failing for 5 minutes
- **What it means**: Significant portion of users affected
- **Action**: Investigate root cause, may need to scale or optimize

**RagErrorsDetected**: > 0.5 RAG errors/sec for 3 minutes
- **What it means**: AI/semantic search having issues
- **Action**: Check Qdrant connection, embedding service health

**SlowResponseTime**: p95 latency > 5 seconds for 5 minutes
- **What it means**: API is slow, users experiencing delays
- **Action**: Check database query performance, external API latency

### Info Alerts (Awareness)

**NewErrorTypeDetected**: New error pattern appeared
- **What it means**: Code is throwing a new type of exception
- **Action**: Review logs to understand new error, may need code fix

## Investigating Errors

### Step 1: Identify the Issue

Start with the **Error Monitoring dashboard**:
1. Check **Error Rate** panel: When did errors spike?
2. Check **Top 10 Endpoints**: Which endpoint is affected?
3. Check **Error Distribution**: What type of errors? (500, 503, timeout?)

### Step 2: Find Correlation ID

In alert notification or Grafana panel:
- Look for **correlation_id** or **TraceId**
- Example: `0HN6G8QJ9KL0M:00000001`

### Step 3: Check Logs in Seq

1. Open Seq: http://localhost:8081
2. Search by correlation ID:
   ```
   RequestId = "0HN6G8QJ9KL0M:00000001"
   ```
3. Look at the full request log:
   - Request method, path, headers
   - User ID, email
   - Exception message and stack trace
   - Request/response payloads

### Step 4: Check Trace in Jaeger

1. Open Jaeger: http://localhost:16686
2. Paste correlation ID in search box
3. View distributed trace:
   - Which service was slow?
   - Where did the error occur?
   - Database query timings
   - External API call timings

### Step 5: Check Metrics in Prometheus

1. Open Prometheus: http://localhost:9090/graph
2. Query specific metrics:
   ```promql
   # Error rate for specific endpoint
   rate(meepleai_api_errors_total{http_route="/api/v1/games"}[5m])

   # RAG errors
   rate(meepleai_rag_errors_total[5m])

   # Response time p95
   histogram_quantile(0.95, rate(http_server_request_duration_bucket[5m]))
   ```

## Common Scenarios

### Scenario 1: Deployment Caused Errors

**Symptoms**:
- Error spike immediately after deployment
- **ErrorSpike** alert fires
- Specific endpoint showing high error rate

**Investigation**:
1. Check git commit SHA of deployed version
2. Review recent code changes to affected endpoint
3. Check logs for new exception types

**Resolution**:
- Rollback deployment: `git revert <commit-sha>`
- Or fix bug and redeploy
- Silence alert during fix

### Scenario 2: Database Connection Issues

**Symptoms**:
- **DatabaseDown** alert fires
- All API endpoints showing 500 errors
- Error category: **dependency**

**Investigation**:
1. Check database container: `docker compose ps postgres`
2. Check database logs: `docker compose logs postgres`
3. Check connection pool: Prometheus metric `db_connection_pool_size`

**Resolution**:
```bash
# Restart database
docker compose restart postgres

# Or restart entire stack
docker compose down && docker compose up -d
```json
### Scenario 3: Slow Performance (No Errors)

**Symptoms**:
- **SlowResponseTime** warning alert
- No error rate increase
- Users complaining about slowness

**Investigation**:
1. Check Grafana → **API Performance** dashboard
2. Identify slow endpoint: p95, p99 latency
3. Check Jaeger trace for slow operation
4. Check Prometheus for resource usage:
   ```promql
   # CPU usage
   rate(process_cpu_seconds_total[5m])

   # Memory usage
   process_working_set_bytes / process_memory_limit_bytes
   ```

**Resolution**:
- Optimize slow database query
- Add caching for expensive operations
- Scale up resources (increase container limits)

### Scenario 4: RAG/AI Errors

**Symptoms**:
- **RagErrorsDetected** warning alert
- Q&A or semantic search failing
- Error category: **dependency** (Qdrant)

**Investigation**:
1. Check Qdrant health: `curl http://localhost:6333/healthz`
2. Check Qdrant logs: `docker compose logs qdrant`
3. Check embedding service (Ollama): `docker compose logs ollama`

**Resolution**:
```bash
# Restart Qdrant
docker compose restart qdrant

# Restart Ollama (if embedding issues)
docker compose restart ollama
```json
## Silencing Alerts

### During Planned Maintenance

**Using Alertmanager UI**:
1. Open http://localhost:9093
2. Click **Silences** → **New Silence**
3. Set matcher:
   - **alertname** = `HighErrorRate` (or specific alert)
   - **service** = `meepleai-api`
4. Set duration: `1h` (or as needed)
5. Add comment: "Deploying v1.2.0"
6. Click **Create**

**Using Grafana OnCall**:
1. Open http://localhost:8082
2. Find alert in "Alerts" page
3. Click **Snooze**
4. Select duration: 30m, 1h, 2h, or custom
5. Add reason: "Maintenance"

### For Known Issues

If an alert is firing for a known issue that can't be fixed immediately:

1. Create GitHub issue to track fix
2. Silence alert in Alertmanager
3. Set expiration: when fix is expected to deploy
4. Link to GitHub issue in silence comment

## Configuration

### Adjusting Alert Thresholds

Edit `infra/prometheus-rules.yml`:

```yaml
# Example: Increase HighErrorRate threshold from 1 to 2 errors/sec
- alert: HighErrorRate
  expr: |
    meepleai:api:error_rate:5m > 2  # Changed from > 1
  for: 2m
```

After changing:
```bash
# Reload Prometheus config
curl -X POST http://localhost:9090/-/reload

# Or restart Prometheus
docker compose restart prometheus
```sql
### Adding Slack Notifications

1. Create Slack webhook: https://api.slack.com/messaging/webhooks
2. Update `infra/alertmanager.yml`:
   ```yaml
   receivers:
     - name: 'critical-alerts'
       slack_configs:
         - api_url: 'https://hooks.slack.com/services/YOUR/WEBHOOK/URL'
           channel: '#alerts'
           title: '🚨 [CRITICAL] {{ .GroupLabels.alertname }}'
   ```
3. Restart Alertmanager:
   ```bash
   docker compose restart alertmanager
   ```

### Adding Email Notifications

Update `infra/alertmanager.yml`:
```yaml
global:
  smtp_smarthost: 'smtp.gmail.com:587'
  smtp_from: 'alertmanager@meepleai.dev'
  smtp_auth_username: 'your-email@gmail.com'
  smtp_auth_password: 'your-app-password'

receivers:
  - name: 'critical-alerts'
    email_configs:
      - to: 'oncall@meepleai.dev'
        headers:
          Subject: '[CRITICAL] {{ .GroupLabels.alertname }}'
```

## Troubleshooting

### Dashboard Shows "No Data"

**Check 1**: Is API running?
```bash
docker compose ps api
curl http://localhost:8080/health
```

**Check 2**: Are metrics exposed?
```bash
curl http://localhost:8080/metrics | grep meepleai_api_errors
```

**Check 3**: Is Prometheus scraping?
- Open http://localhost:9090/targets
- Find `meepleai-api` target
- Should show **UP** in green

**Check 4**: Is Grafana connected to Prometheus?
- Open http://localhost:3001/datasources
- Click Prometheus datasource
- Click **Test** button
- Should show "Data source is working"

### Alert Not Firing

**Check 1**: Is the condition met?
- Open http://localhost:9090/graph
- Run the alert query (e.g., `meepleai:api:error_rate:5m > 1`)
- Does it return a value > threshold?

**Check 2**: Has `for` duration passed?
- Check alert definition in `prometheus-rules.yml`
- If `for: 2m`, alert must be true for 2 minutes before firing
- Check http://localhost:9090/alerts for "Pending" state

**Check 3**: Is Alertmanager configured?
- Open http://localhost:9093
- Should show Alertmanager UI
- Check Status → Config
- Verify receivers are configured

### Too Many Alerts (Alert Fatigue)

**Solution 1**: Increase thresholds
- Edit `prometheus-rules.yml`
- Increase threshold or `for` duration
- Reload Prometheus config

**Solution 2**: Improve alert grouping
- Edit `alertmanager.yml`
- Add more labels to `group_by`
- Increase `group_interval`

**Solution 3**: Add inhibition rules
- Edit `alertmanager.yml`
- Suppress warning alerts when critical alerts fire
- Example already in config: critical inhibits warning

## Best Practices

### For Developers

1. **Test error paths**: Ensure exceptions are properly caught and logged
2. **Add context to exceptions**: Include relevant data in exception messages
3. **Use appropriate status codes**: 400 for client errors, 500 for server errors
4. **Avoid swallowing exceptions**: Let middleware handle them for metrics

### For On-Call Engineers

1. **Check dashboard first**: Get overview before diving into logs
2. **Follow correlation IDs**: Use them to trace requests across systems
3. **Document resolutions**: Add comments to silences explaining what was done
4. **Update runbooks**: If you encounter something not covered, add it

### For Team

1. **Review alerts weekly**: Check for noisy alerts that need tuning
2. **Monitor alert frequency**: Track how often each alert fires
3. **Update thresholds**: Adjust based on actual traffic patterns
4. **Test alert flow**: Periodically trigger test alerts to verify notifications work

## Resources

- **Technical Design**: `docs/ops-05-error-monitoring-design.md`
- **Runbooks**: `docs/runbooks/` (high-error-rate.md, error-spike.md, dependency-down.md)
- **OPS-01 (Logging)**: `docs/observability.md`
- **OPS-02 (Tracing)**: `docs/ops-02-opentelemetry-design.md`

## Quick Reference

| Component | URL | Purpose |
|-----------|-----|---------|
| Grafana Dashboard | http://localhost:3001/d/meepleai-error-monitoring | View error metrics and trends |
| Prometheus Alerts | http://localhost:9090/alerts | See alert states (pending, firing) |
| Alertmanager | http://localhost:9093 | Manage alerts, silences |
| Grafana OnCall | http://localhost:8082 | On-call management, escalations |
| Seq Logs | http://localhost:8081 | Search detailed error logs |
| Jaeger Traces | http://localhost:16686 | View distributed traces |
| API Metrics | http://localhost:8080/metrics | Raw Prometheus metrics |
| API Health | http://localhost:8080/health | Service health checks |

## Support

For questions or issues with error monitoring:
1. Check this guide first
2. Review technical design doc
3. Ask in #engineering Slack channel
4. Create GitHub issue if bug found
