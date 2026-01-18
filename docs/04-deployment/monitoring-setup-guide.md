# MeepleAI Monitoring Setup Guide

**Version**: 1.0
**Last Updated**: 2026-01-18
**Estimated Time**: 2-3 hours
**Cost**: €0 (self-hosted on existing VPS)

---

## Table of Contents

1. [Overview](#1-overview)
2. [Grafana Setup](#2-grafana-setup)
3. [Prometheus Configuration](#3-prometheus-configuration)
4. [Exporters Setup](#4-exporters-setup)
5. [Dashboard Configuration](#5-dashboard-configuration)
6. [Alerting Rules](#6-alerting-rules)
7. [Performance Monitoring](#7-performance-monitoring)
8. [Troubleshooting](#8-troubleshooting)

---

## 1. Overview

### 1.1 Monitoring Stack

**Components**:
```
Grafana (Visualization)
    ↓ queries
Prometheus (Time-Series DB)
    ↓ scrapes
Exporters (Metrics Collection)
    ├─ node-exporter (System: CPU, RAM, Disk)
    ├─ postgres-exporter (Database metrics)
    ├─ redis-exporter (Cache metrics)
    └─ API /metrics endpoint (Application metrics)
```

**Ports**:
| Service | Port | Access |
|---------|------|--------|
| Grafana | 3000 (internal) | SSH tunnel or Traefik proxy |
| Prometheus | 9090 (internal) | Local only |
| Node Exporter | 9100 (internal) | Prometheus scrapes |
| PostgreSQL Exporter | 9187 (internal) | Prometheus scrapes |
| Redis Exporter | 9121 (internal) | Prometheus scrapes |
| API Metrics | 8080/metrics (internal) | Prometheus scrapes |

---

### 1.2 Key Metrics to Monitor

**Infrastructure Metrics** (node-exporter):
- CPU usage (%, per core)
- Memory usage (%, MB)
- Disk usage (%, MB/s I/O)
- Network traffic (MB/s in/out)

**Database Metrics** (postgres-exporter):
- Connection pool usage (active/max)
- Query response time (p50, p95, p99)
- Cache hit ratio (%)
- Replication lag (seconds)

**Application Metrics** (API /metrics):
- HTTP request rate (req/sec)
- HTTP response time (ms, p50/p95/p99)
- Error rate (5xx responses %)
- RAG query confidence scores

**Cache Metrics** (redis-exporter):
- Cache hit rate (%)
- Memory usage (MB)
- Keys count
- Evicted keys rate

---

## 2. Grafana Setup

### 2.1 Start Grafana Container

**Verify docker-compose.yml includes Grafana**:
```yaml
# infra/docker-compose.yml
services:
  grafana:
    image: grafana/grafana:10.2.3
    container_name: meepleai-grafana
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_USER=admin
      - GF_SECURITY_ADMIN_PASSWORD=${GRAFANA_ADMIN_PASSWORD}
      - GF_SERVER_ROOT_URL=https://grafana.meepleai.com
      - GF_INSTALL_PLUGINS=redis-datasource
    volumes:
      - grafana-data:/var/lib/grafana
      - ./monitoring/grafana/provisioning:/etc/grafana/provisioning
    networks:
      - monitoring
    restart: unless-stopped

volumes:
  grafana-data:
    driver: local

networks:
  monitoring:
    driver: bridge
```

---

**Generate Admin Password**:
```bash
# Generate secure password
openssl rand -base64 24 | tr -d '/+=' | head -c 20

# Example output: K3mP9xL2nQ5wR8vT4zY7

# Add to infra/secrets/monitoring.secret
echo "GRAFANA_ADMIN_PASSWORD=K3mP9xL2nQ5wR8vT4zY7" >> infra/secrets/monitoring.secret
```

---

**Start Grafana**:
```bash
cd infra

# Start Grafana + Prometheus
docker compose up -d grafana prometheus

# Verify running
docker ps | grep grafana

# Check logs
docker compose logs grafana | tail -20
```

**Expected Output**: `"HTTP Server Listen" logger=http.server address=:3000`

---

### 2.2 Access Grafana

**Option 1: SSH Tunnel** (Secure, Recommended):
```bash
# From local machine
ssh -L 3000:localhost:3000 meepleai@95.217.163.246

# Keep terminal open, then browse to:
# http://localhost:3000
```

**Option 2: Traefik Reverse Proxy** (Public Access):
```yaml
# docker-compose.yml - Add Traefik labels to Grafana
services:
  grafana:
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.grafana.rule=Host(`grafana.meepleai.com`)"
      - "traefik.http.routers.grafana.entrypoints=websecure"
      - "traefik.http.routers.grafana.tls.certresolver=letsencrypt"
      - "traefik.http.services.grafana.loadbalancer.server.port=3000"
```

**DNS Record** (Cloudflare):
```
Type: A
Name: grafana
IPv4: [YOUR_VPS_IP]
Proxy: DNS Only (gray cloud)
```

**Access**: https://grafana.meepleai.com

---

### 2.3 Initial Grafana Configuration

**First Login**:
1. Navigate: http://localhost:3000 (via SSH tunnel)
2. **Login**:
   - Username: `admin`
   - Password: [Value from GRAFANA_ADMIN_PASSWORD]
3. **Change Password**: Prompted on first login (optional: keep same password)

---

**Configure Data Source**:
1. **Navigate**: Configuration (⚙️) → Data Sources → Add data source
2. **Select**: Prometheus
3. **Configuration**:
   - Name: `Prometheus`
   - URL: `http://prometheus:9090`
   - Access: `Server (default)`
   - Scrape interval: `15s`
4. **Save & Test**: Should show ✅ "Data source is working"

---

## 3. Prometheus Configuration

### 3.1 Prometheus Setup

**Configuration File**: `infra/monitoring/prometheus/prometheus.yml`

```yaml
# Global settings
global:
  scrape_interval: 15s
  evaluation_interval: 15s
  external_labels:
    cluster: 'meepleai-alpha'
    environment: 'production'

# Alert rules
rule_files:
  - '/etc/prometheus/alerts/*.yml'

# Scrape targets
scrape_configs:
  # Prometheus self-monitoring
  - job_name: 'prometheus'
    static_configs:
      - targets: ['localhost:9090']

  # Node Exporter (system metrics)
  - job_name: 'node'
    static_configs:
      - targets: ['node-exporter:9100']
        labels:
          instance: 'meepleai-alpha-01'

  # PostgreSQL Exporter
  - job_name: 'postgresql'
    static_configs:
      - targets: ['postgres-exporter:9187']
        labels:
          database: 'meepleai'

  # Redis Exporter
  - job_name: 'redis'
    static_configs:
      - targets: ['redis-exporter:9121']
        labels:
          cache: 'primary'

  # .NET API Metrics
  - job_name: 'api'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['api:8080']
        labels:
          service: 'api'

  # Qdrant (built-in metrics)
  - job_name: 'qdrant'
    static_configs:
      - targets: ['qdrant:6333']
        labels:
          service: 'vector-db'

  # Traefik (reverse proxy metrics)
  - job_name: 'traefik'
    static_configs:
      - targets: ['traefik:8080']
```

---

**Start Prometheus**:
```bash
cd infra
docker compose up -d prometheus

# Verify running
docker compose logs prometheus | grep -i "server is ready"

# Check targets
curl http://localhost:9090/api/v1/targets | jq '.data.activeTargets[] | {job, health}'
```

**Expected Output**: All targets show `"health": "up"`

---

### 3.2 Alert Rules Configuration

**Create Alert Rules**: `infra/monitoring/prometheus/alerts/api-alerts.yml`

```yaml
groups:
  - name: api_alerts
    interval: 30s
    rules:
      # High Error Rate
      - alert: HighErrorRate
        expr: |
          rate(http_requests_total{status=~"5.."}[5m]) / rate(http_requests_total[5m]) > 0.05
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "High error rate detected"
          description: "API error rate is {{ $value | humanizePercentage }} (threshold: 5%)"

      # High API Latency
      - alert: HighAPILatency
        expr: |
          histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m])) > 1.0
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "API response time degraded"
          description: "P95 latency is {{ $value }}s (threshold: 1s)"

      # API Down
      - alert: APIDown
        expr: up{job="api"} == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "API is down"
          description: "API service is not responding"

  - name: database_alerts
    interval: 30s
    rules:
      # Database Down
      - alert: DatabaseDown
        expr: pg_up == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "PostgreSQL is down"
          description: "Database service is unavailable"

      # High Connection Pool Usage
      - alert: HighConnectionPoolUsage
        expr: |
          (pg_stat_activity_count / pg_settings_max_connections) > 0.80
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "Database connection pool nearly exhausted"
          description: "Connection pool at {{ $value | humanizePercentage }} (threshold: 80%)"

      # Slow Queries
      - alert: SlowQueries
        expr: |
          rate(pg_stat_statements_mean_exec_time_seconds[5m]) > 0.5
        for: 15m
        labels:
          severity: warning
        annotations:
          summary: "Slow database queries detected"
          description: "Average query time is {{ $value }}s"

  - name: system_alerts
    interval: 30s
    rules:
      # High CPU Usage
      - alert: HighCPUUsage
        expr: |
          100 - (avg by (instance) (rate(node_cpu_seconds_total{mode="idle"}[5m])) * 100) > 80
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "High CPU usage"
          description: "CPU usage is {{ $value }}% (threshold: 80%)"

      # High Memory Usage
      - alert: HighMemoryUsage
        expr: |
          (node_memory_MemTotal_bytes - node_memory_MemAvailable_bytes) / node_memory_MemTotal_bytes > 0.85
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "High memory usage"
          description: "Memory usage is {{ $value | humanizePercentage }}"

      # Low Disk Space
      - alert: LowDiskSpace
        expr: |
          (node_filesystem_avail_bytes{mountpoint="/"} / node_filesystem_size_bytes{mountpoint="/"}) < 0.20
        for: 30m
        labels:
          severity: warning
        annotations:
          summary: "Low disk space"
          description: "Only {{ $value | humanizePercentage }} disk space remaining"

  - name: redis_alerts
    interval: 30s
    rules:
      # Low Cache Hit Rate
      - alert: LowCacheHitRate
        expr: |
          rate(redis_keyspace_hits_total[5m]) / (rate(redis_keyspace_hits_total[5m]) + rate(redis_keyspace_misses_total[5m])) < 0.70
        for: 15m
        labels:
          severity: warning
        annotations:
          summary: "Redis cache hit rate low"
          description: "Cache hit rate is {{ $value | humanizePercentage }} (threshold: 70%)"

      # Redis Down
      - alert: RedisDown
        expr: up{job="redis"} == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Redis is down"
          description: "Cache service unavailable"
```

---

## 4. Exporters Setup

### 4.1 Node Exporter (System Metrics)

**docker-compose.yml**:
```yaml
services:
  node-exporter:
    image: prom/node-exporter:v1.7.0
    container_name: meepleai-node-exporter
    command:
      - '--path.rootfs=/host'
      - '--collector.filesystem.mount-points-exclude=^/(sys|proc|dev|host|etc)($$|/)'
    volumes:
      - '/:/host:ro,rslave'
    ports:
      - "9100:9100"
    networks:
      - monitoring
    restart: unless-stopped
```

**Start**:
```bash
docker compose up -d node-exporter

# Verify metrics
curl http://localhost:9100/metrics | head -20
```

**Expected Metrics**:
- `node_cpu_seconds_total`
- `node_memory_MemTotal_bytes`
- `node_filesystem_size_bytes`

---

### 4.2 PostgreSQL Exporter

**docker-compose.yml**:
```yaml
services:
  postgres-exporter:
    image: prometheuscommunity/postgres-exporter:v0.15.0
    container_name: meepleai-postgres-exporter
    environment:
      DATA_SOURCE_NAME: "postgresql://meepleai:${POSTGRES_PASSWORD}@postgres:5432/meepleai_db?sslmode=disable"
      PG_EXPORTER_EXTEND_QUERY_PATH: "/etc/postgres-exporter/queries.yaml"
    volumes:
      - ./monitoring/postgres-exporter/queries.yaml:/etc/postgres-exporter/queries.yaml:ro
    ports:
      - "9187:9187"
    networks:
      - monitoring
    depends_on:
      - postgres
    restart: unless-stopped
```

---

**Custom Queries**: `infra/monitoring/postgres-exporter/queries.yaml`

```yaml
pg_stat_statements:
  query: |
    SELECT
      queryid,
      query,
      calls,
      mean_exec_time,
      stddev_exec_time,
      rows
    FROM pg_stat_statements
    WHERE query NOT LIKE '%pg_stat_statements%'
    ORDER BY mean_exec_time DESC
    LIMIT 10
  metrics:
    - queryid:
        usage: "LABEL"
        description: "Query ID"
    - calls:
        usage: "COUNTER"
        description: "Number of times executed"
    - mean_exec_time:
        usage: "GAUGE"
        description: "Mean execution time in milliseconds"

pg_database_size:
  query: |
    SELECT
      datname,
      pg_database_size(datname) as size_bytes
    FROM pg_database
    WHERE datname = 'meepleai_db'
  metrics:
    - datname:
        usage: "LABEL"
        description: "Database name"
    - size_bytes:
        usage: "GAUGE"
        description: "Database size in bytes"

pg_table_sizes:
  query: |
    SELECT
      schemaname || '.' || tablename AS table_name,
      pg_total_relation_size(schemaname||'.'||tablename) AS total_bytes,
      pg_relation_size(schemaname||'.'||tablename) AS data_bytes,
      pg_indexes_size(schemaname||'.'||tablename) AS index_bytes
    FROM pg_tables
    WHERE schemaname = 'public'
    ORDER BY total_bytes DESC
    LIMIT 20
  metrics:
    - table_name:
        usage: "LABEL"
    - total_bytes:
        usage: "GAUGE"
    - data_bytes:
        usage: "GAUGE"
    - index_bytes:
        usage: "GAUGE"
```

**Start**:
```bash
docker compose up -d postgres-exporter

# Test metrics
curl http://localhost:9187/metrics | grep pg_up
# Expected: pg_up 1
```

---

### 4.3 Redis Exporter

**docker-compose.yml**:
```yaml
services:
  redis-exporter:
    image: oliver006/redis_exporter:v1.56.0
    container_name: meepleai-redis-exporter
    environment:
      - REDIS_ADDR=redis://redis:6379
      - REDIS_PASSWORD=${REDIS_PASSWORD}
    ports:
      - "9121:9121"
    networks:
      - monitoring
    depends_on:
      - redis
    restart: unless-stopped
```

**Start**:
```bash
docker compose up -d redis-exporter

# Test
curl http://localhost:9121/metrics | grep redis_up
# Expected: redis_up 1
```

---

### 4.4 API Metrics Endpoint (.NET)

**Already Configured** in MeepleAI API via `Observability/MetricsService.cs`

**Verify Endpoint**:
```bash
curl http://localhost:8080/metrics

# Expected output (Prometheus format):
# http_requests_total{method="GET",endpoint="/health",status="200"} 42
# http_request_duration_seconds_bucket{endpoint="/api/v1/games",le="0.1"} 125
# rag_query_confidence{game="catan"} 0.87
```

**Custom Metrics in Code**:
```csharp
// Example: Track RAG confidence scores
using Prometheus;

private static readonly Histogram RagConfidence = Metrics
    .CreateHistogram("rag_query_confidence", "RAG response confidence scores",
        new HistogramConfiguration
        {
            Buckets = new[] { 0.5, 0.6, 0.7, 0.8, 0.9, 0.95, 1.0 },
            LabelNames = new[] { "game", "query_type" }
        });

// In RAG handler
RagConfidence.WithLabels(gameName, "rules").Observe(confidenceScore);
```

---

## 5. Dashboard Configuration

### 5.1 Import Pre-Built Dashboards

**Grafana UI** → Dashboards → Import:

**1. Node Exporter Full** (System Metrics):
- Dashboard ID: `1860`
- Data Source: Prometheus
- Import

**Panels Included**:
- CPU usage (per core, total)
- Memory usage (used, cached, buffers)
- Disk I/O (read/write MB/s)
- Network traffic (inbound/outbound)
- Disk space (per partition)

---

**2. PostgreSQL Database** (Database Metrics):
- Dashboard ID: `9628`
- Data Source: Prometheus
- Import

**Panels**:
- Active connections
- Query duration (p50, p95, p99)
- Cache hit ratio
- Transaction rate
- Database size growth

---

**3. Redis Dashboard**:
- Dashboard ID: `11835`
- Data Source: Prometheus
- Import

**Panels**:
- Memory usage
- Cache hit rate
- Commands per second
- Evicted keys
- Connected clients

---

**4. Docker Container Monitoring**:
- Dashboard ID: `193`
- Data Source: Prometheus
- Import

---

### 5.2 Create Custom MeepleAI Dashboard

**Grafana** → Dashboards → New Dashboard → Add Visualization

**Panel 1: API Request Rate**:
```promql
# Query
rate(http_requests_total[5m])

# Panel Settings:
- Title: "API Requests per Second"
- Visualization: Time series
- Legend: {{method}} {{endpoint}}
```

---

**Panel 2: API Response Time (P95)**:
```promql
# Query
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))

# Panel Settings:
- Title: "API Response Time (P95)"
- Visualization: Time series
- Unit: seconds (s)
- Thresholds:
    - Green: < 0.3s
    - Yellow: 0.3-1.0s
    - Red: > 1.0s
```

---

**Panel 3: Error Rate**:
```promql
# Query
rate(http_requests_total{status=~"5.."}[5m]) / rate(http_requests_total[5m])

# Panel Settings:
- Title: "Error Rate (%)"
- Visualization: Stat (single value)
- Unit: Percent (0.0-1.0)
- Thresholds:
    - Green: < 0.01 (1%)
    - Yellow: 0.01-0.05 (1-5%)
    - Red: > 0.05 (>5%)
```

---

**Panel 4: Database Connection Pool**:
```promql
# Query 1 (Active Connections)
pg_stat_activity_count

# Query 2 (Max Connections)
pg_settings_max_connections

# Panel Settings:
- Title: "PostgreSQL Connection Pool"
- Visualization: Gauge
- Min: 0
- Max: from pg_settings_max_connections
- Thresholds:
    - Green: 0-70%
    - Yellow: 70-85%
    - Red: 85-100%
```

---

**Panel 5: RAG Query Confidence**:
```promql
# Query (Average confidence last 5 min)
avg(rate(rag_query_confidence_sum[5m]) / rate(rag_query_confidence_count[5m]))

# Panel Settings:
- Title: "RAG Confidence Score (Avg)"
- Visualization: Gauge
- Unit: Percent (0.0-1.0)
- Thresholds:
    - Red: < 0.60
    - Yellow: 0.60-0.75
    - Green: > 0.75
```

---

**Panel 6: Cache Hit Rate**:
```promql
# Query
rate(redis_keyspace_hits_total[5m]) / (rate(redis_keyspace_hits_total[5m]) + rate(redis_keyspace_misses_total[5m]))

# Panel Settings:
- Title: "Redis Cache Hit Rate"
- Visualization: Stat
- Unit: Percent
- Target: > 80%
```

---

**Save Dashboard**:
1. Click "Save dashboard" (💾 icon)
2. Name: `MeepleAI - Production Overview`
3. Folder: General
4. Tags: `meepleai`, `production`, `alpha`
5. Save

---

### 5.3 Dashboard Organization

**Recommended Dashboard Structure**:

**1. Overview Dashboard** (Home):
- API request rate, error rate, latency
- Database connection pool, query time
- System CPU, RAM, Disk
- Cache hit rate

**2. API Performance**:
- Request rate by endpoint
- Response time percentiles (p50, p75, p90, p95, p99)
- Error rate by endpoint
- Request size distribution

**3. Database Metrics**:
- Connection pool usage
- Query performance (top 10 slowest)
- Cache hit ratio
- Replication lag (if replicas enabled)
- Table sizes

**4. System Resources**:
- CPU usage (per core)
- Memory breakdown (used, cached, free)
- Disk I/O (read/write operations)
- Network bandwidth (in/out)

**5. Business Metrics** (custom):
- Active users (concurrent)
- RAG queries per hour
- PDF uploads per day
- User registrations per day
- Subscription conversions (Free → Normal → Premium)

---

## 6. Alerting Rules

### 6.1 Alertmanager Setup

**docker-compose.yml**:
```yaml
services:
  alertmanager:
    image: prom/alertmanager:v0.26.0
    container_name: meepleai-alertmanager
    command:
      - '--config.file=/etc/alertmanager/config.yml'
      - '--storage.path=/alertmanager'
    volumes:
      - ./monitoring/alertmanager/config.yml:/etc/alertmanager/config.yml:ro
      - alertmanager-data:/alertmanager
    ports:
      - "9093:9093"
    networks:
      - monitoring
    restart: unless-stopped

volumes:
  alertmanager-data:
```

---

**Configuration**: `infra/monitoring/alertmanager/config.yml`

```yaml
global:
  resolve_timeout: 5m

route:
  group_by: ['alertname', 'severity']
  group_wait: 10s
  group_interval: 10s
  repeat_interval: 12h
  receiver: 'default'

  routes:
    # Critical alerts → Email immediately
    - match:
        severity: critical
      receiver: 'email-critical'
      repeat_interval: 1h

    # Warning alerts → Email after 30 min
    - match:
        severity: warning
      receiver: 'email-warning'
      repeat_interval: 4h

receivers:
  - name: 'default'
    webhook_configs:
      - url: 'http://localhost:5001/webhook'

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

          Time: {{ .StartsAt }}

  - name: 'email-warning'
    email_configs:
      - to: 'admin@meepleai.com'
        from: 'alerts@meepleai.com'
        smarthost: 'smtp.sendgrid.net:587'
        auth_username: 'apikey'
        auth_password: '${SENDGRID_API_KEY}'
        headers:
          Subject: '⚠️ WARNING: {{ .GroupLabels.alertname }}'

inhibit_rules:
  # Inhibit warning if critical alert active
  - source_match:
      severity: 'critical'
    target_match:
      severity: 'warning'
    equal: ['alertname', 'instance']
```

---

**Update Prometheus Config**:
```yaml
# prometheus.yml - Add Alertmanager
alerting:
  alertmanagers:
    - static_configs:
        - targets: ['alertmanager:9093']
```

**Restart**:
```bash
docker compose restart prometheus alertmanager
```

---

### 6.2 Test Alert

**Trigger Test Alert**:
```bash
# Simulate high error rate
for i in {1..100}; do
  curl -s https://api.meepleai.com/nonexistent-endpoint > /dev/null
done

# Wait 5 minutes for alert to fire

# Check Alertmanager
curl http://localhost:9093/api/v2/alerts
```

**Expected**: Email received with alert details

---

### 6.3 Slack Integration (Optional)

**Alternative to Email**: Send alerts to Slack channel

**Configuration**:
```yaml
# alertmanager/config.yml
receivers:
  - name: 'slack'
    slack_configs:
      - api_url: 'https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXX'
        channel: '#meepleai-alerts'
        title: '{{ .GroupLabels.alertname }}'
        text: |
          *Alert:* {{ .GroupLabels.alertname }}
          *Severity:* {{ .CommonLabels.severity }}
          *Description:* {{ .CommonAnnotations.description }}
          *Time:* {{ .StartsAt }}
        color: '{{ if eq .CommonLabels.severity "critical" }}danger{{ else }}warning{{ end }}'
```

**Create Slack Webhook**:
1. Slack → Apps → Incoming Webhooks
2. Add to Workspace → Choose #meepleai-alerts channel
3. Copy Webhook URL → Add to alertmanager config

---

## 7. Performance Monitoring

### 7.1 Key Performance Indicators (KPIs)

**Target SLAs** (Alpha Phase):
| Metric | Target | Alert Threshold |
|--------|--------|-----------------|
| API Uptime | 95%+ | <95% for 1h |
| API Latency (P95) | <500ms | >1s for 10min |
| Error Rate | <5% | >5% for 5min |
| Database Query Time (P95) | <100ms | >500ms for 15min |
| Cache Hit Rate | >70% | <70% for 15min |

---

**Dashboard Panel Setup**:

**SLA Compliance Panel**:
```promql
# Uptime (last 24h)
avg_over_time(up{job="api"}[24h]) * 100

# P95 Latency
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))

# Error Rate
rate(http_requests_total{status=~"5.."}[5m]) / rate(http_requests_total[5m]) * 100
```

---

### 7.2 Custom Business Metrics

**Track in Application Code**:

```csharp
// Metrics.cs
using Prometheus;

public static class AppMetrics
{
    // User registrations
    public static readonly Counter Registrations = Metrics
        .CreateCounter("user_registrations_total", "Total user registrations");

    // PDF uploads
    public static readonly Counter PdfUploads = Metrics
        .CreateCounter("pdf_uploads_total", "Total PDF uploads",
            new CounterConfiguration { LabelNames = new[] { "user_tier" } });

    // RAG queries
    public static readonly Histogram RagQueryDuration = Metrics
        .CreateHistogram("rag_query_duration_seconds", "RAG query processing time",
            new HistogramConfiguration
            {
                Buckets = new[] { 0.1, 0.5, 1.0, 2.0, 5.0, 10.0 },
                LabelNames = new[] { "game_id" }
            });

    // Subscription tier changes
    public static readonly Counter TierUpgrades = Metrics
        .CreateCounter("subscription_upgrades_total", "Tier upgrade events",
            new CounterConfiguration { LabelNames = new[] { "from_tier", "to_tier" } });
}

// Usage in code
AppMetrics.Registrations.Inc();
AppMetrics.PdfUploads.WithLabels("premium").Inc();
AppMetrics.RagQueryDuration.WithLabels(gameId).Observe(duration.TotalSeconds);
AppMetrics.TierUpgrades.WithLabels("free", "normal").Inc();
```

---

**Grafana Panel - User Registrations**:
```promql
# Query
rate(user_registrations_total[1h]) * 3600

# Panel Settings:
- Title: "User Registrations per Hour"
- Visualization: Graph
- Y-axis: Registrations/hour
```

---

**Grafana Panel - Subscription Conversions**:
```promql
# Query
sum(rate(subscription_upgrades_total{to_tier="normal"}[1d])) by (from_tier)

# Panel Settings:
- Title: "Free → Normal Conversions (Daily)"
- Visualization: Stat
```

---

### 7.3 Performance Baseline Recording

**First Week After Deployment**:

**Record Baseline Metrics** (save to runbook):
```bash
# Average API response time
curl http://localhost:9090/api/v1/query?query=histogram_quantile(0.95,%20rate(http_request_duration_seconds_bucket[24h]))

# Average CPU usage
curl http://localhost:9090/api/v1/query?query=100%20-%20avg(rate(node_cpu_seconds_total{mode=%22idle%22}[24h]))%20*%20100

# Average memory usage
curl http://localhost:9090/api/v1/query?query=(node_memory_MemTotal_bytes%20-%20node_memory_MemAvailable_bytes)%20/%20node_memory_MemTotal_bytes%20*%20100

# Database size
curl http://localhost:9090/api/v1/query?query=pg_database_size_bytes
```

**Example Baselines** (Alpha with 10 users):
| Metric | Baseline Value | Notes |
|--------|---------------|-------|
| API P95 Latency | 180ms | Target: <500ms ✅ |
| CPU Usage | 25% | Target: <70% ✅ |
| RAM Usage | 65% | Target: <85% ✅ |
| Database Size | 85MB | Growth: ~5MB/week |
| Cache Hit Rate | 82% | Target: >70% ✅ |

**Use baselines for**:
- Capacity planning (when to scale)
- Performance regression detection
- Anomaly alerting

---

## 8. Troubleshooting

### 8.1 Grafana Issues

**Issue**: Grafana not accessible

**Diagnosis**:
```bash
# Check container running
docker ps | grep grafana

# Check logs
docker compose logs grafana | tail -50

# Common errors:
# - "Failed to connect to database" → Volume permission issue
# - "Port 3000 already in use" → Another process using port
```

**Fix**:
```bash
# Restart Grafana
docker compose restart grafana

# Check port conflicts
sudo lsof -i :3000

# Reset Grafana data (WARNING: loses dashboards)
docker compose down grafana
docker volume rm infra_grafana-data
docker compose up -d grafana
```

---

**Issue**: Data source not working

**Diagnosis**:
```bash
# Test Prometheus reachability from Grafana container
docker exec meepleai-grafana wget -O- http://prometheus:9090/api/v1/targets

# Should return JSON with targets
```

**Fix**:
- Verify Prometheus running: `docker ps | grep prometheus`
- Check network: Both containers in same `monitoring` network
- Update data source URL: `http://prometheus:9090` (not localhost)

---

### 8.2 Prometheus Issues

**Issue**: Metrics not collected

**Diagnosis**:
```bash
# Check Prometheus targets
curl http://localhost:9090/api/v1/targets | jq '.data.activeTargets[] | {job, health, lastError}'

# Common errors:
# - "dial tcp: connect: connection refused" → Exporter not running
# - "context deadline exceeded" → Slow scrape (increase timeout)
# - "401 Unauthorized" → Authentication required
```

**Fix**:
```bash
# Restart exporters
docker compose restart node-exporter postgres-exporter redis-exporter

# Verify exporter responding
curl http://localhost:9100/metrics | head -10  # Node exporter
curl http://localhost:9187/metrics | head -10  # PostgreSQL exporter

# Check Prometheus config syntax
docker exec meepleai-prometheus promtool check config /etc/prometheus/prometheus.yml
```

---

**Issue**: High Prometheus memory usage

**Diagnosis**:
```bash
# Check Prometheus memory
docker stats meepleai-prometheus --no-stream

# Check TSDB size
du -sh /var/lib/docker/volumes/prometheus-data/_data/
```

**Fix**:
```yaml
# prometheus.yml - Reduce retention
global:
  scrape_interval: 30s  # Increase from 15s (reduce data points)

storage:
  tsdb:
    retention.time: 15d  # Reduce from 30d
    retention.size: 5GB  # Limit disk usage
```

---

### 8.3 Missing Metrics

**Issue**: Custom application metrics not appearing

**Diagnosis**:
```bash
# Test API /metrics endpoint
curl http://localhost:8080/metrics

# Should see:
# http_requests_total{...}
# http_request_duration_seconds_bucket{...}
# rag_query_confidence{...}

# If empty or 404:
# - API not exposing /metrics
# - Prometheus middleware not registered
```

**Fix** (.NET API):
```csharp
// Program.cs - Verify metrics endpoint registered
app.UseMetricServer();  // Exposes /metrics endpoint
app.UseHttpMetrics();   // Tracks HTTP requests

// Or manually map endpoint
app.MapGet("/metrics", async context =>
{
    await context.Response.WriteAsync(await Metrics.DefaultRegistry.CollectAndSerializeAsync());
});
```

---

## 9. Monitoring Best Practices

### 9.1 Dashboard Review Schedule

**Daily** (during Alpha/Beta):
- [ ] Check error rate (target: <1%)
- [ ] Review slow query log (PostgreSQL)
- [ ] Verify backup completed (check logs)

**Weekly**:
- [ ] Review resource trends (CPU, RAM growth)
- [ ] Analyze top endpoints by traffic
- [ ] Check cache hit rate optimization opportunities
- [ ] Review alert history (false positives?)

**Monthly**:
- [ ] Capacity planning review (when to scale?)
- [ ] Database maintenance (VACUUM, ANALYZE)
- [ ] Update baseline metrics
- [ ] Archive old Prometheus data (>30 days)

---

### 9.2 Alert Tuning

**Reduce False Positives**:

**Problem**: Too many alerts for transient issues

**Solution**: Increase `for` duration
```yaml
# Before (too sensitive)
- alert: HighCPU
  expr: node_cpu_usage > 0.70
  for: 1m  # Fires on brief spike

# After (better)
- alert: HighCPU
  expr: node_cpu_usage > 0.70
  for: 10m  # Only sustained high CPU
```

---

**Problem**: Alert storm (100+ alerts in 1 minute)

**Solution**: Use `group_wait` and `group_interval`
```yaml
route:
  group_wait: 30s      # Wait 30s to batch alerts
  group_interval: 5m   # Group new alerts every 5 min
  repeat_interval: 12h # Don't repeat resolved alerts for 12h
```

---

### 9.3 Metric Retention Policy

**Prometheus Storage**:
```yaml
# prometheus.yml
storage:
  tsdb:
    retention.time: 30d    # Keep 30 days of raw data
    retention.size: 10GB   # Or limit by size (whichever hits first)
```

**Downsampling Strategy** (future optimization):
```
Raw data (15s interval): 7 days
1-minute aggregates: 30 days
5-minute aggregates: 90 days
1-hour aggregates: 1 year
```

**Tools**: Thanos, VictoriaMetrics (for long-term storage)

---

## 10. Quick Reference

### 10.1 Important URLs

| Service | URL (SSH Tunnel) | URL (Public, if configured) |
|---------|-----------------|----------------------------|
| Grafana | http://localhost:3000 | https://grafana.meepleai.com |
| Prometheus | http://localhost:9090 | - (internal only) |
| Alertmanager | http://localhost:9093 | - (internal only) |
| Node Exporter | http://localhost:9100/metrics | - |
| PostgreSQL Exporter | http://localhost:9187/metrics | - |

---

### 10.2 Common PromQL Queries

**API Performance**:
```promql
# Request rate
sum(rate(http_requests_total[5m])) by (endpoint)

# Error percentage
sum(rate(http_requests_total{status=~"5.."}[5m])) / sum(rate(http_requests_total[5m])) * 100

# P95 latency
histogram_quantile(0.95, sum(rate(http_request_duration_seconds_bucket[5m])) by (le, endpoint))

# Throughput (requests per minute)
sum(rate(http_requests_total[1m])) * 60
```

**Database**:
```promql
# Active connections
pg_stat_activity_count

# Connection pool usage %
(pg_stat_activity_count / pg_settings_max_connections) * 100

# Database size (GB)
pg_database_size_bytes / 1024 / 1024 / 1024

# Cache hit ratio
pg_stat_database_blks_hit / (pg_stat_database_blks_hit + pg_stat_database_blks_read)
```

**System**:
```promql
# CPU usage %
100 - (avg by (instance) (rate(node_cpu_seconds_total{mode="idle"}[5m])) * 100)

# Memory usage %
(node_memory_MemTotal_bytes - node_memory_MemAvailable_bytes) / node_memory_MemTotal_bytes * 100

# Disk usage %
(node_filesystem_size_bytes{mountpoint="/"} - node_filesystem_avail_bytes{mountpoint="/"}) / node_filesystem_size_bytes{mountpoint="/"} * 100

# Network traffic (MB/s)
rate(node_network_receive_bytes_total[5m]) / 1024 / 1024
```

---

### 10.3 Grafana Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `G` + `H` | Go to Home dashboard |
| `G` + `D` | Go to Dashboards list |
| `G` + `E` | Go to Explore (raw PromQL) |
| `Ctrl/Cmd` + `S` | Save dashboard |
| `D` + `K` | Toggle kiosk mode (TV display) |
| `T` + `Z` | Zoom out time range |
| `T` + `←` | Move time range back |

---

## 11. Advanced Configuration

### 11.1 Recording Rules (Pre-Computed Metrics)

**Purpose**: Pre-compute expensive queries for faster dashboard loading

**Configuration**: `infra/monitoring/prometheus/rules/recording-rules.yml`

```yaml
groups:
  - name: api_recording_rules
    interval: 30s
    rules:
      # Pre-compute API error rate
      - record: api:error_rate:5m
        expr: |
          sum(rate(http_requests_total{status=~"5.."}[5m])) by (endpoint)
          /
          sum(rate(http_requests_total[5m])) by (endpoint)

      # Pre-compute P95 latency
      - record: api:latency:p95:5m
        expr: |
          histogram_quantile(0.95,
            sum(rate(http_request_duration_seconds_bucket[5m])) by (le, endpoint)
          )

      # Pre-compute database connection pool usage
      - record: db:connection_pool_usage:ratio
        expr: |
          pg_stat_activity_count / pg_settings_max_connections
```

**Usage in Dashboards** (faster rendering):
```promql
# Before (expensive query)
histogram_quantile(0.95, sum(rate(http_request_duration_seconds_bucket[5m])) by (le, endpoint))

# After (uses pre-computed recording rule)
api:latency:p95:5m
```

---

### 11.2 Grafana Variables (Dynamic Dashboards)

**Create Variable**:
1. Dashboard Settings → Variables → Add variable
2. **Configuration**:
   - Name: `environment`
   - Type: Query
   - Data source: Prometheus
   - Query: `label_values(up, environment)`
   - Multi-value: Yes
   - Include All option: Yes

**Use in Panels**:
```promql
# Query with variable
rate(http_requests_total{environment="$environment"}[5m])
```

**Benefits**: Single dashboard for Alpha, Beta, Production environments

---

### 11.3 Annotations (Event Markers)

**Mark Deployments on Dashboards**:

```bash
# After deployment, create annotation
curl -X POST http://localhost:3000/api/annotations \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer ${GRAFANA_API_KEY}" \
  -d '{
    "dashboardId": 1,
    "time": '$(date +%s000)',
    "text": "Deployment v1.2.3",
    "tags": ["deployment", "release"]
  }'
```

**Manual Annotation**:
1. Click on graph at specific time
2. Add annotation → "Deployed v1.2.3"
3. Useful for correlating performance changes with deployments

---

## 12. Monitoring Checklist

### Pre-Launch Verification

- [ ] Grafana accessible (http://localhost:3000)
- [ ] Prometheus targets all "UP" (http://localhost:9090/targets)
- [ ] Node exporter metrics present (`node_cpu_seconds_total`)
- [ ] PostgreSQL exporter connected (`pg_up == 1`)
- [ ] Redis exporter metrics available (`redis_up == 1`)
- [ ] API metrics endpoint working (curl localhost:8080/metrics)
- [ ] At least 1 dashboard configured (System Overview)
- [ ] Alert rules loaded (Prometheus → Alerts page)
- [ ] Alertmanager configured (test email sent)

---

### Weekly Monitoring Tasks

- [ ] Review error rate trends (should be declining)
- [ ] Check for new slow queries (PostgreSQL exporter)
- [ ] Verify backup completion (check logs)
- [ ] Review resource usage growth (predict scaling needs)
- [ ] Test alert notifications (trigger test alert)
- [ ] Update dashboard thresholds if baseline changed

---

### Monthly Monitoring Tasks

- [ ] Archive Prometheus data (>30 days to S3/Glacier)
- [ ] Review alert history (tune thresholds to reduce false positives)
- [ ] Update capacity planning based on growth trends
- [ ] Clean up unused exporters/metrics (reduce Prometheus load)
- [ ] Audit access logs (who accessed Grafana)
- [ ] Review monitoring costs (storage growth)

---

## 13. Cost of Monitoring

**Resource Usage** (Alpha/Beta):
| Service | RAM | CPU | Storage | Included in VPS Cost |
|---------|-----|-----|---------|---------------------|
| Grafana | 500MB | 0.1 core | 1GB | ✅ Yes |
| Prometheus | 1GB | 0.2 core | 5GB (30d retention) | ✅ Yes |
| Node Exporter | 50MB | 0.05 core | Negligible | ✅ Yes |
| PostgreSQL Exporter | 100MB | 0.05 core | Negligible | ✅ Yes |
| Redis Exporter | 50MB | 0.05 core | Negligible | ✅ Yes |
| **Total** | **1.7GB** | **0.45 core** | **6GB** | **€0 extra** |

**Impact on VPS**:
- Alpha VPS (CPX31): 16GB RAM → 1.7GB monitoring = **10.6% RAM overhead** (acceptable)
- Beta VPS (CCX33): 32GB RAM → 1.7GB = **5.3% overhead** (negligible)

**Conclusion**: ✅ **Monitoring is essentially free** (uses spare VPS resources)

---

**Managed Monitoring Alternative** (Release 10K):
| Service | Provider | Cost | Use Case |
|---------|----------|------|----------|
| Grafana Cloud | Grafana Labs | €0-49/mese | 10K series, 50GB logs |
| Datadog | Datadog | €15/host/mese | Full observability |
| New Relic | New Relic | €25/user/mese | APM + infrastructure |

**Recommendation**: Self-hosted sufficient until Release 10K (€0 vs €180-300/mese)

---

## 14. Next Steps

**After Monitoring Setup**:
1. [ ] Configure baseline metrics (record first week performance)
2. [ ] Create runbook with common issues + fixes
3. [ ] Setup on-call rotation (who gets alerts?)
4. [ ] Document dashboard usage for team
5. [ ] Schedule weekly monitoring review meetings

**Integration with**:
- [Infrastructure Deployment Checklist](./infrastructure-deployment-checklist.md) - Include monitoring in Phase 6
- [Health Checks](./health-checks.md) - Integrate health endpoints with Prometheus

---

**Reference Links**:
- Grafana Documentation: https://grafana.com/docs/grafana/latest/
- Prometheus Query Examples: https://prometheus.io/docs/prometheus/latest/querying/examples/
- Dashboard Marketplace: https://grafana.com/grafana/dashboards/
- PromQL Tutorial: https://prometheus.io/docs/prometheus/latest/querying/basics/

