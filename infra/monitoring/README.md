# MeepleAI Monitoring Infrastructure

Observability stack: Prometheus (metrics), Loki (logs), Grafana (visualization), HyperDX (unified observability).

## Services

| Service | Port | Purpose |
|---------|------|---------|
| Grafana | 3001 | Visualization & dashboards |
| Prometheus | 9090 | Metrics storage & querying |
| Loki | 3100 | Log aggregation & querying |
| Fluent Bit | - | Log collection agent |
| HyperDX | 8180 | Unified observability (logs + traces + session replay) |
| Alertmanager | 9093 | Alert routing & notifications |

## Quick Start

```bash
# Start metrics stack
docker compose up -d grafana prometheus

# Start log aggregation (Issue #3367)
docker compose -f docker-compose.logging.yml up -d

# Start HyperDX (optional - unified observability)
docker compose -f docker-compose.hyperdx.yml up -d

# Verify all healthy
docker ps --filter "name=meepleai-" --format "{{.Names}}\t{{.Status}}"
```

## Directory Structure

```
monitoring/
├── grafana/
│   └── dashboards/          # Pre-built JSON dashboards
│       ├── epic-4068-complete-dashboard.json
│       ├── k6-load-testing.json
│       ├── multi-tier-cache-performance.json
│       └── shared-catalog-performance.json
├── loki/
│   └── loki-config.yml      # Loki configuration (30-day retention)
├── fluent-bit/
│   ├── fluent-bit.conf      # Fluent Bit inputs/outputs
│   └── parsers.conf         # Log parsers (.NET, Python)
└── README.md                # This file
```

## Configuration Files

### Loki (`loki/loki-config.yml`)
- Storage: Filesystem (`/loki`)
- Retention: 30 days (720h)
- Schema: v13 TSDB
- Limits: 10MB/s ingestion rate, 10K streams per user

### Fluent Bit (`fluent-bit/`)
- **fluent-bit.conf**: Collects from Docker containers, forwards to Loki
- **parsers.conf**: JSON parsers for .NET (Serilog) and Python services

## Grafana Dashboards

Import dashboards from `grafana/dashboards/*.json`:
1. Grafana → Dashboards → Import
2. Upload JSON file
3. Select data sources (Prometheus, Loki)

## Data Sources Configuration

### Prometheus
- URL: `http://prometheus:9090`
- Scrape interval: 15s
- Retention: 30 days

### Loki
- URL: `http://loki:3100`
- Max query lookback: 30 days

### HyperDX (if using)
- Web UI: `http://localhost:8180`
- OTLP HTTP: `http://meepleai-hyperdx:14318`

## Alerting

Configured via `prometheus/alert.rules.yml` (not shown, if exists).

Common alerts:
- High error rate (>10 errors/5min)
- Slow API responses (p95 >500ms)
- Low cache hit rate (<70%)
- High memory usage (>85%)

## Querying Logs

### Via Grafana Explore
1. Grafana → Explore (compass icon)
2. Select **Loki** data source
3. Use LogQL:
   ```logql
   {container_name="meepleai-api"} |= "ERROR"
   ```

### Via CLI (logcli)
```bash
# Install logcli
docker run grafana/logcli:latest --addr=http://localhost:3100 query '{container_name="meepleai-api"}'
```

## Related Documentation

- [Monitoring Quick Start](../../docs/04-deployment/monitoring-quickstart.md)
- [Monitoring Reference](../../docs/04-deployment/monitoring-reference.md)
- [Log Aggregation Guide](../../docs/04-deployment/log-aggregation-guide.md)
