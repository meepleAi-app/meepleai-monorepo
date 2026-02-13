# MeepleAI Monitoring Quick Start

**Version**: 1.0 | **Est. Time**: 2-3 hours | **Cost**: €0 (self-hosted)

---

## 1. Setup Checklist

### Prerequisites
- [ ] Docker & docker-compose installed
- [ ] VPS access (Alpha: CPX31, Beta: CCX33)
- [ ] Admin credentials generated

### Core Services
- [ ] Start Grafana container
- [ ] Start Prometheus container
- [ ] Configure exporters (node, postgres, redis)
- [ ] Verify API /metrics endpoint

### Configuration
- [ ] Add Prometheus data source to Grafana
- [ ] Import pre-built dashboards
- [ ] Configure alert rules
- [ ] Setup Alertmanager notifications

---

## 2. Quick Start Commands

### Generate Admin Password
```bash
# Generate secure password
openssl rand -base64 24 | tr -d '/+=' | head -c 20

# Add to secrets
echo "GRAFANA_ADMIN_PASSWORD=<generated>" >> infra/secrets/monitoring.secret
```

### Start Monitoring Stack
```bash
cd infra

# Start all monitoring services
docker compose up -d grafana prometheus node-exporter postgres-exporter redis-exporter

# Verify all running
docker ps | grep -E 'grafana|prometheus|exporter'

# Check Prometheus targets
curl http://localhost:9090/api/v1/targets | jq '.data.activeTargets[] | {job, health}'
```

### Access Grafana
```bash
# SSH tunnel (recommended)
ssh -L 3000:localhost:3000 user@95.217.163.246

# Browse to http://localhost:3000
# Login: admin / <GRAFANA_ADMIN_PASSWORD>
```

---

## 3. Essential Configuration

### Add Prometheus Data Source
1. Grafana → Configuration (⚙️) → Data Sources → Add data source
2. Select **Prometheus**
3. Configure:
   - **URL**: `http://prometheus:9090`
   - **Access**: Server (default)
   - **Scrape interval**: 15s
4. Save & Test → ✅ "Data source is working"

### Import Pre-Built Dashboards
| Dashboard | ID | Purpose |
|-----------|----|---------|
| Node Exporter Full | `1860` | System metrics (CPU, RAM, Disk) |
| PostgreSQL Database | `9628` | Database performance |
| Redis Dashboard | `11835` | Cache metrics |
| Docker Containers | `193` | Container monitoring |

**Import Steps**: Dashboards → Import → Enter ID → Select Prometheus → Import

---

## 4. Critical Metrics to Monitor

### SLA Targets (Alpha Phase)
| Metric | Target | Alert Threshold |
|--------|--------|-----------------|
| API Uptime | 95%+ | <95% for 1h |
| API Latency (P95) | <500ms | >1s for 10min |
| Error Rate | <5% | >5% for 5min |
| DB Query Time (P95) | <100ms | >500ms for 15min |
| Cache Hit Rate | >70% | <70% for 15min |

### Key Metric Groups
**Infrastructure** (node-exporter):
- CPU usage (%, per core)
- Memory usage (%, MB)
- Disk I/O (MB/s)
- Network traffic (MB/s)

**Database** (postgres-exporter):
- Connection pool usage
- Query response time (p50, p95, p99)
- Cache hit ratio
- Database size

**Application** (API /metrics):
- HTTP request rate
- Response time percentiles
- Error rate (5xx)
- RAG query confidence

**Cache** (redis-exporter):
- Cache hit rate
- Memory usage
- Evicted keys
- Connected clients

---

## 5. Alert Rules Quick Setup

### Create Alert File
**File**: `infra/monitoring/prometheus/alerts/critical-alerts.yml`

```yaml
groups:
  - name: critical_alerts
    interval: 30s
    rules:
      # API Down
      - alert: APIDown
        expr: up{job="api"} == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "API is down"

      # High Error Rate
      - alert: HighErrorRate
        expr: |
          rate(http_requests_total{status=~"5.."}[5m])
          / rate(http_requests_total[5m]) > 0.05
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Error rate > 5%"

      # Database Down
      - alert: DatabaseDown
        expr: pg_up == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "PostgreSQL unavailable"

      # High CPU
      - alert: HighCPU
        expr: |
          100 - (avg(rate(node_cpu_seconds_total{mode="idle"}[5m])) * 100) > 80
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "CPU usage > 80%"
```

### Enable Alerts
```bash
# Reload Prometheus config
docker compose restart prometheus

# Verify alerts loaded
curl http://localhost:9090/api/v1/rules | jq '.data.groups[].rules[].name'
```

---

## 6. Verification Tests

### Test Exporters
```bash
# Node exporter
curl http://localhost:9100/metrics | grep node_cpu

# PostgreSQL exporter
curl http://localhost:9187/metrics | grep pg_up

# Redis exporter
curl http://localhost:9121/metrics | grep redis_up

# API metrics
curl http://localhost:8080/metrics | grep http_requests_total
```

### Test Alert
```bash
# Trigger high error rate
for i in {1..100}; do
  curl -s https://api.meepleai.com/nonexistent > /dev/null
done

# Wait 5 min, check Alertmanager
curl http://localhost:9093/api/v2/alerts
```

---

## 7. Maintenance Routines

### Daily Checks
- [ ] Error rate < 1%
- [ ] Slow query log review
- [ ] Backup completion

### Weekly Tasks
- [ ] Resource trend analysis
- [ ] Cache hit rate optimization
- [ ] Alert history review
- [ ] Top endpoints analysis

### Monthly Tasks
- [ ] Capacity planning
- [ ] Database VACUUM/ANALYZE
- [ ] Update baselines
- [ ] Archive old metrics (>30d)

---

## 8. Quick Troubleshooting

### Grafana Issues
```bash
# Restart Grafana
docker compose restart grafana

# Check logs
docker compose logs grafana | tail -50

# Test Prometheus connection
docker exec meepleai-grafana wget -O- http://prometheus:9090/api/v1/targets
```

### Prometheus Issues
```bash
# Check targets
curl http://localhost:9090/api/v1/targets | jq '.data.activeTargets[] | {job, health}'

# Restart exporters
docker compose restart node-exporter postgres-exporter redis-exporter

# Validate config
docker exec meepleai-prometheus promtool check config /etc/prometheus/prometheus.yml
```

### Missing Metrics
```bash
# Verify API endpoint
curl http://localhost:8080/metrics

# Check Prometheus scrape errors
docker compose logs prometheus | grep -i error
```

---

## 9. Resource Impact

**Monitoring Overhead** (Alpha VPS):
| Service | RAM | CPU | Storage |
|---------|-----|-----|---------|
| Grafana | 500MB | 0.1 core | 1GB |
| Prometheus | 1GB | 0.2 core | 5GB |
| Exporters | 200MB | 0.15 core | Negligible |
| **Total** | **1.7GB** | **0.45 core** | **6GB** |

**Impact**: 10.6% RAM on CPX31 (16GB) → ✅ Acceptable

---

## 10. Access URLs

| Service | SSH Tunnel | Public (if configured) |
|---------|-----------|------------------------|
| Grafana | http://localhost:3000 | https://grafana.meepleai.com |
| Prometheus | http://localhost:9090 | - (internal) |
| Node Exporter | http://localhost:9100/metrics | - |
| PostgreSQL Exporter | http://localhost:9187/metrics | - |
| Redis Exporter | http://localhost:9121/metrics | - |

---

## 11. Next Steps

After basic setup:
1. [ ] Record baseline metrics (first week)
2. [ ] Create custom MeepleAI dashboard
3. [ ] Configure email/Slack alerts
4. [ ] Document team runbook
5. [ ] Schedule monitoring reviews

**References**:
- [Monitoring Reference](./monitoring-reference.md) - Detailed metrics, queries, dashboards
- [Infrastructure Deployment](./infrastructure-deployment-checklist.md) - Phase 6 integration
- Grafana Docs: https://grafana.com/docs/grafana/latest/
