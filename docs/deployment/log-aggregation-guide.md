# Log Aggregation Guide - Loki + Fluent Bit

**Issue**: #3367 | **Stack**: Loki 3.0, Fluent Bit 3.0, Grafana

## Overview

MeepleAI uses Loki for centralized log aggregation from all services:
- .NET API (Serilog → Fluent Bit → Loki)
- Python services (stdout → Fluent Bit → Loki)
- All Docker containers (Fluent Bit → Loki)

## Architecture

```
┌─────────────┐  ┌──────────────┐  ┌─────────────────┐
│ .NET API    │  │ Python svcs  │  │ All containers  │
│ (Serilog)   │  │ (stdout)     │  │ (Docker logs)   │
└──────┬──────┘  └──────┬───────┘  └────────┬────────┘
       │                │                   │
       └────────────────┴───────────────────┘
                        │
                 ┌──────▼─────────┐
                 │  Fluent Bit    │
                 │  (collector)   │
                 └──────┬─────────┘
                        │
                 ┌──────▼─────────┐
                 │     Loki       │
                 │  (storage)     │
                 │  :3100         │
                 └──────┬─────────┘
                        │
                 ┌──────▼─────────┐
                 │    Grafana     │
                 │  (visualization│
                 │     :3001)     │
                 └────────────────┘
```

## Quick Start

### Start Services

```bash
cd infra

# Start log aggregation stack
docker compose -f compose.logging.yml up -d

# Verify Loki is ready
curl http://localhost:3100/ready

# Verify Fluent Bit is collecting
docker logs meepleai-fluent-bit --tail 50
```

### Add Loki Data Source to Grafana

1. Grafana → Configuration → Data Sources → Add data source
2. Select **Loki**
3. URL: `http://loki:3100`
4. Save & Test

## LogQL Queries

### Basic Queries

**All logs from API**:
```logql
{container_name="meepleai-api"}
```

**Error logs only**:
```logql
{container_name="meepleai-api"} |= "ERROR"
```

**Logs from Python services**:
```logql
{container_name=~"meepleai-(embedding|smoldocling|unstructured).*"}
```

**Last 5 minutes of warnings/errors**:
```logql
{environment="production"} |~ "WARN|ERROR" [5m]
```

### Structured Log Parsing

**Extract .NET log level**:
```logql
{container_name="meepleai-api"} | json | line_format "{{.Level}}: {{.MessageTemplate}}"
```

**Filter by correlation ID**:
```logql
{container_name="meepleai-api"} | json | CorrelationId="abc-123"
```

**Aggregate error count per service**:
```logql
sum by (container_name) (
  count_over_time({environment="production"} |= "ERROR" [1h])
)
```

### Advanced Queries

**Slow API requests (>500ms)**:
```logql
{container_name="meepleai-api"}
  | json
  | line_format "{{.Properties.ElapsedMilliseconds}}ms: {{.Properties.RequestPath}}"
  | unwrap Properties_ElapsedMilliseconds
  | Properties_ElapsedMilliseconds > 500
```

**PDF processing errors**:
```logql
{container_name=~"meepleai-(smoldocling|unstructured).*"} |= "ERROR" | json | Stage != ""
```

**Rate of 500 errors in last hour**:
```logql
rate({container_name="meepleai-api"} |= "StatusCode=500" [1h])
```

## Grafana Dashboard Setup

### Create Log Dashboard

1. **Grafana** → Dashboards → New Dashboard
2. **Add Panel** → Select Loki data source
3. **Query examples**:
   - Error rate by service (graph)
   - Recent errors (table)
   - Log volume by level (bar chart)

### Pre-Built Dashboard Imports

- **Loki Dashboard**: Grafana ID `13639`
- **Fluent Bit Metrics**: Grafana ID `7752`

## Alert Rules

### Error Spike Alert

```yaml
# In Loki ruler config
groups:
  - name: application_errors
    interval: 1m
    rules:
      - alert: HighErrorRate
        expr: |
          sum(rate({container_name="meepleai-api"} |= "ERROR" [5m])) > 10
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High error rate in API"
          description: "API error rate is {{ $value }} errors/sec"
```

## Retention & Cleanup

**Retention**: 30 days (720h) configured in `loki-config.yml`

**Manual cleanup**:
```bash
# Check Loki storage size
docker exec meepleai-loki du -sh /loki

# Compact old data (automatic via compactor, or manual)
docker exec meepleai-loki loki-cli compact /loki
```

## Troubleshooting

| Issue | Check | Fix |
|-------|-------|-----|
| No logs in Loki | `docker logs meepleai-fluent-bit` | Verify container log paths |
| Fluent Bit parse errors | Check parsers.conf | Update JSON parser format |
| Loki disk full | Check retention policy | Reduce retention or add disk space |
| Missing container logs | Verify Docker socket mount | Check Fluent Bit volume mounts |

## API Reference

**Loki HTTP API**:
- Health: `GET http://localhost:3100/ready`
- Query: `GET http://localhost:3100/loki/api/v1/query`
- Labels: `GET http://localhost:3100/loki/api/v1/labels`
- Metrics: `GET http://localhost:3100/metrics`

**LogQL Documentation**: https://grafana.com/docs/loki/latest/query/

## Related Documentation

- [Monitoring Reference](./monitoring-reference.md) - Complete PromQL queries
- [Log Generation Test Plan](../05-testing/backend/log-generation-test-plan.md) - Backend log testing
