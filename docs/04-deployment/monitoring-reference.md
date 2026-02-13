# MeepleAI Monitoring Reference

**Version**: 1.0 | **Companion to**: [monitoring-quickstart.md](./monitoring-quickstart.md)

---

## 1. Metrics Reference

### Infrastructure Metrics (node-exporter)

| Metric | PromQL | Description | Alert Threshold |
|--------|--------|-------------|-----------------|
| CPU Usage | `100 - (avg(rate(node_cpu_seconds_total{mode="idle"}[5m])) * 100)` | Total CPU % | >80% for 10min |
| Memory Usage | `(node_memory_MemTotal_bytes - node_memory_MemAvailable_bytes) / node_memory_MemTotal_bytes * 100` | RAM % | >85% for 10min |
| Disk Usage | `(node_filesystem_size_bytes - node_filesystem_avail_bytes) / node_filesystem_size_bytes * 100` | Disk % | >80% for 30min |
| Disk I/O | `rate(node_disk_io_time_seconds_total[5m])` | I/O utilization | >90% for 15min |
| Network RX | `rate(node_network_receive_bytes_total[5m]) / 1024 / 1024` | MB/s inbound | Baseline +200% |
| Network TX | `rate(node_network_transmit_bytes_total[5m]) / 1024 / 1024` | MB/s outbound | Baseline +200% |

### Database Metrics (postgres-exporter)

| Metric | PromQL | Description | Alert Threshold |
|--------|--------|-------------|-----------------|
| DB Up | `pg_up` | 1=up, 0=down | == 0 for 1min |
| Active Connections | `pg_stat_activity_count` | Current connections | >80% of max |
| Connection Pool % | `(pg_stat_activity_count / pg_settings_max_connections) * 100` | Pool usage | >80% for 10min |
| Cache Hit Ratio | `pg_stat_database_blks_hit / (pg_stat_database_blks_hit + pg_stat_database_blks_read)` | % queries from cache | <90% for 15min |
| Query Time (Avg) | `rate(pg_stat_statements_mean_exec_time_seconds[5m])` | Mean query duration | >500ms for 15min |
| Database Size | `pg_database_size_bytes / 1024 / 1024 / 1024` | Size in GB | Monitor growth |
| Deadlocks | `rate(pg_stat_database_deadlocks[5m])` | Deadlocks/sec | >0 sustained |

### Application Metrics (API /metrics)

| Metric | PromQL | Description | Alert Threshold |
|--------|--------|-------------|-----------------|
| Request Rate | `rate(http_requests_total[5m])` | Requests/sec | Baseline ±300% |
| Error Rate % | `rate(http_requests_total{status=~"5.."}[5m]) / rate(http_requests_total[5m]) * 100` | 5xx errors % | >5% for 5min |
| Latency P50 | `histogram_quantile(0.50, rate(http_request_duration_seconds_bucket[5m]))` | Median response | >300ms sustained |
| Latency P95 | `histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))` | 95th percentile | >1s for 10min |
| Latency P99 | `histogram_quantile(0.99, rate(http_request_duration_seconds_bucket[5m]))` | 99th percentile | >3s for 10min |
| RAG Confidence | `avg(rate(rag_query_confidence_sum[5m]) / rate(rag_query_confidence_count[5m]))` | Avg confidence | <0.60 for 15min |

### Cache Metrics (redis-exporter)

| Metric | PromQL | Description | Alert Threshold |
|--------|--------|-------------|-----------------|
| Redis Up | `redis_up` | 1=up, 0=down | == 0 for 1min |
| Hit Rate % | `rate(redis_keyspace_hits_total[5m]) / (rate(redis_keyspace_hits_total[5m]) + rate(redis_keyspace_misses_total[5m])) * 100` | Cache hit % | <70% for 15min |
| Memory Usage | `redis_memory_used_bytes / 1024 / 1024` | Used MB | >90% of max |
| Evicted Keys | `rate(redis_evicted_keys_total[5m])` | Keys/sec evicted | >100/sec sustained |
| Connected Clients | `redis_connected_clients` | Active connections | >max_clients * 0.9 |
| Commands/sec | `rate(redis_commands_processed_total[5m])` | Throughput | Monitor baseline |

---

## 2. Dashboard Templates

### System Overview Dashboard

**Panels**:
1. **API Request Rate** (Time series): `rate(http_requests_total[5m])`
2. **Error Rate** (Stat): `rate(http_requests_total{status=~"5.."}[5m]) / rate(http_requests_total[5m]) * 100`
3. **API Latency P95** (Gauge): `histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))`
4. **CPU Usage** (Graph): `100 - (avg(rate(node_cpu_seconds_total{mode="idle"}[5m])) * 100)`
5. **Memory Usage** (Graph): `(node_memory_MemTotal_bytes - node_memory_MemAvailable_bytes) / node_memory_MemTotal_bytes * 100`
6. **DB Connections** (Gauge): `pg_stat_activity_count`
7. **Cache Hit Rate** (Stat): `rate(redis_keyspace_hits_total[5m]) / (rate(redis_keyspace_hits_total[5m]) + rate(redis_keyspace_misses_total[5m])) * 100`

### Pre-Built Dashboard IDs

| Dashboard | ID | Data Source | Description |
|-----------|----|--------------|--------------|
| Node Exporter Full | `1860` | Prometheus | Complete system metrics |
| PostgreSQL Database | `9628` | Prometheus | DB performance & health |
| Redis Dashboard | `11835` | Prometheus | Cache metrics & monitoring |
| Docker Containers | `193` | Prometheus | Container resource usage |

**Import**: Dashboards → Import → Enter ID → Select Prometheus

---

## 3. Alert Rule Reference

### Critical Alerts (severity: critical)

```yaml
# API Down (1min tolerance)
- alert: APIDown
  expr: up{job="api"} == 0
  for: 1m

# Database Down (1min tolerance)
- alert: DatabaseDown
  expr: pg_up == 0
  for: 1m

# High Error Rate (>5% for 5min)
- alert: HighErrorRate
  expr: |
    rate(http_requests_total{status=~"5.."}[5m])
    / rate(http_requests_total[5m]) > 0.05
  for: 5m

# Redis Down (1min tolerance)
- alert: RedisDown
  expr: up{job="redis"} == 0
  for: 1m
```

### Warning Alerts (severity: warning)

```yaml
# High CPU (>80% for 10min)
- alert: HighCPU
  expr: |
    100 - (avg(rate(node_cpu_seconds_total{mode="idle"}[5m])) * 100) > 80
  for: 10min

# High Memory (>85% for 10min)
- alert: HighMemory
  expr: |
    (node_memory_MemTotal_bytes - node_memory_MemAvailable_bytes)
    / node_memory_MemTotal_bytes > 0.85
  for: 10min

# Low Disk Space (<20% free for 30min)
- alert: LowDiskSpace
  expr: |
    (node_filesystem_avail_bytes / node_filesystem_size_bytes) < 0.20
  for: 30min

# High API Latency (P95 >1s for 10min)
- alert: HighAPILatency
  expr: |
    histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m])) > 1.0
  for: 10min

# Low Cache Hit Rate (<70% for 15min)
- alert: LowCacheHitRate
  expr: |
    rate(redis_keyspace_hits_total[5m])
    / (rate(redis_keyspace_hits_total[5m]) + rate(redis_keyspace_misses_total[5m])) < 0.70
  for: 15min

# High DB Connection Pool (>80% for 10min)
- alert: HighConnectionPoolUsage
  expr: |
    (pg_stat_activity_count / pg_settings_max_connections) > 0.80
  for: 10min
```

---

## 4. Common PromQL Queries

### API Performance Queries

```promql
# Total request rate
sum(rate(http_requests_total[5m]))

# Request rate by endpoint
sum(rate(http_requests_total[5m])) by (endpoint)

# Request rate by method
sum(rate(http_requests_total[5m])) by (method)

# Success rate (non-5xx)
sum(rate(http_requests_total{status!~"5.."}[5m]))
/ sum(rate(http_requests_total[5m])) * 100

# Throughput (requests per minute)
sum(rate(http_requests_total[1m])) * 60

# Request duration percentiles
histogram_quantile(0.50, sum(rate(http_request_duration_seconds_bucket[5m])) by (le))  # P50
histogram_quantile(0.95, sum(rate(http_request_duration_seconds_bucket[5m])) by (le))  # P95
histogram_quantile(0.99, sum(rate(http_request_duration_seconds_bucket[5m])) by (le))  # P99
```

### Database Queries

```promql
# Database size growth (last 24h)
increase(pg_database_size_bytes[24h]) / 1024 / 1024  # MB

# Top 10 slowest queries
topk(10, pg_stat_statements_mean_exec_time_seconds)

# Table sizes (top 10)
topk(10, pg_table_sizes_total_bytes)

# Index sizes
sum(pg_table_sizes_index_bytes) by (table_name)
```

### System Resource Queries

```promql
# CPU per core
100 - (avg by (cpu) (rate(node_cpu_seconds_total{mode="idle"}[5m])) * 100)

# Memory breakdown
node_memory_MemTotal_bytes
node_memory_MemAvailable_bytes
node_memory_Cached_bytes
node_memory_Buffers_bytes

# Disk I/O rate (MB/s)
rate(node_disk_read_bytes_total[5m]) / 1024 / 1024   # Read
rate(node_disk_written_bytes_total[5m]) / 1024 / 1024  # Write

# Network bandwidth (MB/s)
rate(node_network_receive_bytes_total[5m]) / 1024 / 1024   # Inbound
rate(node_network_transmit_bytes_total[5m]) / 1024 / 1024  # Outbound
```

---

## 5. Custom Business Metrics

### Application Code Examples

```csharp
using Prometheus;

// User registrations
public static readonly Counter Registrations = Metrics
    .CreateCounter("user_registrations_total", "Total user registrations");

// PDF uploads by tier
public static readonly Counter PdfUploads = Metrics
    .CreateCounter("pdf_uploads_total", "Total PDF uploads",
        new CounterConfiguration { LabelNames = new[] { "user_tier" } });

// RAG query duration
public static readonly Histogram RagQueryDuration = Metrics
    .CreateHistogram("rag_query_duration_seconds", "RAG query processing time",
        new HistogramConfiguration
        {
            Buckets = new[] { 0.1, 0.5, 1.0, 2.0, 5.0, 10.0 },
            LabelNames = new[] { "game_id" }
        });

// Subscription upgrades
public static readonly Counter TierUpgrades = Metrics
    .CreateCounter("subscription_upgrades_total", "Tier upgrade events",
        new CounterConfiguration { LabelNames = new[] { "from_tier", "to_tier" } });
```

### Business Metric Queries

```promql
# User registrations per hour
rate(user_registrations_total[1h]) * 3600

# PDF uploads by tier (last 24h)
sum(increase(pdf_uploads_total[24h])) by (user_tier)

# RAG query P95 latency by game
histogram_quantile(0.95, sum(rate(rag_query_duration_seconds_bucket[5m])) by (le, game_id))

# Free → Premium conversion rate (daily)
sum(rate(subscription_upgrades_total{from_tier="free", to_tier="premium"}[1d]))
```

---

## 6. Recording Rules (Pre-Computed Metrics)

```yaml
groups:
  - name: api_recording_rules
    interval: 30s
    rules:
      # Pre-compute error rate
      - record: api:error_rate:5m
        expr: |
          sum(rate(http_requests_total{status=~"5.."}[5m])) by (endpoint)
          / sum(rate(http_requests_total[5m])) by (endpoint)

      # Pre-compute P95 latency
      - record: api:latency:p95:5m
        expr: |
          histogram_quantile(0.95,
            sum(rate(http_request_duration_seconds_bucket[5m])) by (le, endpoint))

      # Pre-compute DB connection pool usage
      - record: db:connection_pool_usage:ratio
        expr: pg_stat_activity_count / pg_settings_max_connections
```

**Usage**: Query `api:latency:p95:5m` instead of full histogram calculation

---

## 7. Baseline Metrics (Alpha Environment)

**Recorded**: Week 1 after Alpha deployment (10 concurrent users)

| Metric | Baseline Value | Target | Notes |
|--------|---------------|--------|-------|
| API P95 Latency | 180ms | <500ms | ✅ Well below target |
| CPU Usage (Avg) | 25% | <70% | ✅ Headroom available |
| RAM Usage (Avg) | 65% | <85% | ✅ Acceptable |
| DB Size | 85MB | Monitor | Growth: ~5MB/week |
| Cache Hit Rate | 82% | >70% | ✅ Above target |
| Error Rate | 0.3% | <5% | ✅ Excellent |
| Uptime (7d) | 99.2% | >95% | ✅ Meets SLA |

**Use for**: Capacity planning, performance regression detection, anomaly alerting

---

## 8. Grafana Shortcuts

| Shortcut | Action |
|----------|--------|
| `G` + `H` | Go to Home dashboard |
| `G` + `D` | Dashboards list |
| `G` + `E` | Explore (PromQL) |
| `Ctrl/Cmd` + `S` | Save dashboard |
| `D` + `K` | Kiosk mode (TV) |
| `T` + `Z` | Zoom out time range |
| `T` + `←/→` | Shift time range |
| `Ctrl/Cmd` + `K` | Command palette |

---

## 9. Alertmanager Configuration

### Email Notification Template

```yaml
receivers:
  - name: 'email-critical'
    email_configs:
      - to: 'admin@meepleai.com'
        from: 'alerts@meepleai.com'
        smarthost: 'smtp.sendgrid.net:587'
        auth_username: 'apikey'
        auth_password: '${SENDGRID_API_KEY}'
        headers:
          Subject: '🚨 CRITICAL: {{ .GroupLabels.alertname }}'
        text: |
          Alert: {{ .GroupLabels.alertname }}
          Severity: {{ .CommonLabels.severity }}
          Description: {{ .CommonAnnotations.description }}
          Instance: {{ .CommonLabels.instance }}
          Time: {{ .StartsAt }}
```

### Slack Notification Template

```yaml
receivers:
  - name: 'slack'
    slack_configs:
      - api_url: 'https://hooks.slack.com/services/YOUR/WEBHOOK/URL'
        channel: '#meepleai-alerts'
        title: '{{ .GroupLabels.alertname }}'
        text: |
          *Severity:* {{ .CommonLabels.severity }}
          *Description:* {{ .CommonAnnotations.description }}
          *Time:* {{ .StartsAt }}
        color: '{{ if eq .CommonLabels.severity "critical" }}danger{{ else }}warning{{ end }}'
```

---

## 10. Performance Tuning

### Prometheus Storage Optimization

```yaml
# prometheus.yml
global:
  scrape_interval: 15s  # Balance: 15s (detailed) vs 30s (less data)
  evaluation_interval: 15s

storage:
  tsdb:
    retention.time: 30d   # Keep 30 days raw data
    retention.size: 10GB  # Or size limit (whichever first)
```

### Downsampling Strategy (Future)

```
Raw data (15s):     7 days
1min aggregates:    30 days
5min aggregates:    90 days
1hour aggregates:   1 year
```

**Tools**: Thanos, VictoriaMetrics for long-term storage

---

## 11. References

- **Grafana Docs**: https://grafana.com/docs/grafana/latest/
- **Prometheus Query Examples**: https://prometheus.io/docs/prometheus/latest/querying/examples/
- **PromQL Tutorial**: https://prometheus.io/docs/prometheus/latest/querying/basics/
- **Dashboard Marketplace**: https://grafana.com/grafana/dashboards/
- **Alertmanager Docs**: https://prometheus.io/docs/alerting/latest/alertmanager/
