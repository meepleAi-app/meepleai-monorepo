# Prometheus Setup & Configuration

**Version**: 2.0 (Consolidated)
**Last Updated**: 2025-12-08
**Status**: Production Ready
**Location**: Consolidated from `infra/prometheus/README.md` + `infra/prometheus/alerts/README.md`

---

## Table of Contents

1. [Overview](#overview)
2. [Installation & Configuration](#installation--configuration)
3. [Alert Rules (40+)](#alert-rules-40)
4. [Metrics Reference](#metrics-reference)
5. [AlertManager Integration](#alertmanager-integration)
6. [Grafana Integration](#grafana-integration)
7. [Best Practices](#best-practices)
8. [Testing Alerts](#testing-alerts)
9. [Troubleshooting](#troubleshooting)
10. [Related Documentation](#related-documentation)

---

## Overview

Prometheus monitoring and alerting configuration for MeepleAI with 40+ modular alert rules organized by category.

### Key Features

- **9 Alert Categories**: API, Cache, Database, Infrastructure, Vector Search, Quality, PDF, Prompts, HTTP Retry
- **40+ Alert Rules**: Critical, Warning, Info severity levels
- **Modular Structure**: Alert rules in separate files by category
- **Grafana Integration**: 8 dashboards using Prometheus metrics
- **AlertManager Routing**: Email, Slack, PagerDuty notifications

### Components

```
infra/prometheus/
├── prometheus.yml              # Main Prometheus configuration
├── prometheus-rules.yml        # Recording rules
└── alerts/                     # Alert rules (9 files)
    ├── api-performance.yml     # API errors, latency, throughput (7 rules)
    ├── cache-performance.yml   # Redis monitoring (3 rules)
    ├── database-health.yml     # PostgreSQL alerts (4 rules)
    ├── infrastructure.yml      # Resource monitoring (5 rules)
    ├── vector-search.yml       # Qdrant performance (3 rules)
    ├── quality-metrics.yml     # RAG quality (8 rules)
    ├── pdf-processing.yml      # PDF pipeline (7 rules)
    ├── prompt-management.yml   # Prompt versioning (3 rules)
    └── http-retry-alerts.yaml  # HTTP retry patterns (5 rules)
```

---

## Installation & Configuration

### Docker Compose Integration

Prometheus is part of the observability profile (Issue #702):

```bash
# Development + basic monitoring
docker compose --profile dev up -d

# Full observability stack
docker compose --profile observability up -d
```

**Access**: http://localhost:9090

### Main Configuration (prometheus.yml)

```yaml
global:
  scrape_interval: 15s        # Scrape metrics every 15s
  evaluation_interval: 15s    # Evaluate alert rules every 15s

scrape_configs:
  - job_name: 'api'
    static_configs:
      - targets: ['api:8080']
    metrics_path: '/metrics'

  - job_name: 'prometheus'
    static_configs:
      - targets: ['localhost:9090']

  - job_name: 'cadvisor'         # Container metrics (Issue #705)
    static_configs:
      - targets: ['cadvisor:8082']

  - job_name: 'node-exporter'    # Host metrics (Issue #705)
    static_configs:
      - targets: ['node-exporter:9100']

rule_files:
  - '/etc/prometheus/alerts/*.yml'
  - '/etc/prometheus/alerts/*.yaml'
```

### Retention & Storage

| Environment | Retention | Max Size | Configuration |
|-------------|-----------|----------|---------------|
| **Development** | 15 days | Unlimited | Default |
| **Staging** | 60 days | Unlimited | `compose.staging.yml` |
| **Production** | 90 days | 50GB | `compose.prod.yml` |

**Configuration Example**:
```yaml
command:
  - '--storage.tsdb.retention.time=90d'
  - '--storage.tsdb.retention.size=50GB'
  - '--config.file=/etc/prometheus/prometheus.yml'
```

---

## Alert Rules (40+)

### API Performance (api-performance.yml) - 7 Rules

**Metrics**: HTTP requests, latency, error rate, throughput

| Alert | Condition | For | Severity | Target |
|-------|-----------|-----|----------|--------|
| **HighApiErrorRate** | 5xx rate >1% | 5m | Critical | <1% |
| **HighApiLatency** | P95 >1s | 10m | Warning | <500ms |
| **VeryHighApiLatency** | P99 >3s | 5m | Critical | <1s |
| **ApiDown** | No response | 1m | Critical | 99.9% uptime |
| **HighRequestRate** | >1000 req/s | 5m | Warning | Monitor abuse |
| **LowSuccessRate** | <95% | 5m | Warning | >95% |
| **ApiMemoryLeakSuspected** | Memory ↑ constant | 30m | Critical | Stable |

**Runbook**: [High Error Rate](../runbooks/high-error-rate.md)

### Cache Performance (cache-performance.yml) - 3 Rules

**Metrics**: Redis cache hit rate, miss rate, latency

| Alert | Condition | For | Severity | Target |
|-------|-----------|-----|----------|--------|
| **LowCacheHitRate** | Hit rate <80% | 10m | Warning | >85% |
| **HighCacheMissRate** | Miss rate >20% | 5m | Info | <15% |
| **RedisDown** | No response | 1m | Critical | Always up |

**Expected Hit Rate**: >85%, <10ms latency

### Database Health (database-health.yml) - 4 Rules

**Metrics**: PostgreSQL connections, query time, transaction rate

| Alert | Condition | For | Severity | Target |
|-------|-----------|-----|----------|--------|
| **HighDatabaseConnections** | Pool >80% | 5m | Warning | <70% |
| **SlowDatabaseQueries** | Avg >500ms | 10m | Warning | <100ms P95 |
| **PostgresDown** | No response | 1m | Critical | Always up |
| **HighDatabaseCPU** | CPU >70% | 10m | Warning | <60% |

**Connection Pool**: Max 100 connections

### Infrastructure (infrastructure.yml) - 5 Rules (Issue #705)

**Metrics**: CPU, memory, disk, network (cAdvisor + node-exporter)

| Alert | Condition | For | Severity | Applies To |
|-------|-----------|-----|----------|------------|
| **HighCpuUsage** | CPU >80% | 10m | Warning | All containers |
| **VeryHighCpuUsage** | CPU >95% | 5m | Critical | All containers |
| **HighMemoryUsage** | Memory >85% | 10m | Warning | All containers |
| **LowDiskSpace** | Disk <10% | 5m | Critical | Host system |
| **HighNetworkTraffic** | >1GB/s | 5m | Info | Network monitoring |

**Runbook**: [Infrastructure Monitoring](../runbooks/infrastructure-monitoring.md)

### Vector Search (vector-search.yml) - 3 Rules

**Metrics**: Qdrant query latency, indexing rate, collection size

| Alert | Condition | For | Severity | Target |
|-------|-----------|-----|----------|--------|
| **HighQdrantLatency** | Latency >500ms | 5m | Warning | <200ms P95 |
| **QdrantDown** | No response | 1m | Critical | Always up |
| **QdrantIndexingBacklog** | Backlog >1000 docs | 10m | Warning | <100 docs |

**Indexing Rate**: Target >100 docs/sec

### Quality Metrics (quality-metrics.yml) - 8 Rules

**Metrics**: RAG confidence, hallucination rate, retrieval metrics

| Alert | Condition | For | Severity | Target |
|-------|-----------|-----|----------|--------|
| **LowRagConfidence** | Avg <0.70 | 1h | Warning | ≥0.70 |
| **VeryLowRagConfidence** | Avg <0.60 | 30m | Critical | ≥0.70 |
| **HighHallucinationRate** | >3% | 30m | Critical | <3% |
| **LowRetrievalPrecision** | P@10 <0.85 | 1h | Warning | ≥0.85 |
| **LowRetrievalMRR** | MRR <0.75 | 1h | Warning | ≥0.75 |
| **HighRetrievalLatency** | >1s | 10m | Warning | <500ms |
| **LowCitationAccuracy** | <95% | 1h | Critical | ≥95% |
| **HighLLMTokenUsage** | >90% limit | 5m | Warning | <80% |

**Quality Targets**: Confidence ≥0.70, Hallucination <3%, P@10 ≥0.85

**Runbook**: [AI Quality Low](../runbooks/ai-quality-low.md)

### PDF Processing (pdf-processing.yml) - 7 Rules

**Metrics**: PDF pipeline success rate, quality score, processing time

| Alert | Condition | For | Severity | Pipeline Stage |
|-------|-----------|-----|----------|----------------|
| **HighPdfProcessingFailureRate** | >5% failures | 1h | Warning | Overall |
| **AllPdfExtractionStagesFailed** | All 3 stages fail | 1m | Critical | All stages |
| **LowPdfQualityScore** | Avg <0.80 | 1h | Warning | Quality |
| **VeryLowPdfQualityScore** | <0.60 | 5m | Critical | Quality |
| **SlowPdfProcessing** | >30s/doc | 10m | Warning | Performance |
| **HighStage1FailureRate** | Unstructured >20% | 1h | Info | Stage 1 |
| **PdfProcessingBacklog** | >10 docs queued | 10m | Warning | Queue |

**3-Stage Pipeline** (ADR-003b):
- Stage 1 (Unstructured): 80% success, 1.3s avg
- Stage 2 (SmolDocling): 15% fallback, 3-5s avg
- Stage 3 (Docnet): 5% fallback, <1s avg

**Overall Target**: >95% success rate, ≥0.80 quality score

### Prompt Management (prompt-management.yml) - 3 Rules

**Metrics**: Prompt template versions, token usage

| Alert | Condition | For | Severity |
|-------|-----------|-----|----------|
| **OutdatedPromptTemplate** | Age >30 days | 1d | Warning |
| **HighPromptTokenUsage** | >90% limit | 5m | Warning |
| **PromptTemplateNotFound** | Template missing | 1m | Critical |

**Best Practice**: Update prompt templates every 2-4 weeks

### HTTP Retry (http-retry-alerts.yaml) - 5 Rules

**Metrics**: HTTP client retry patterns, circuit breaker status

| Alert | Condition | For | Severity |
|-------|-----------|-----|----------|
| **HighHttpRetryRate** | >10% retries | 10m | Warning |
| **HttpCircuitBreakerOpen** | Circuit open | 1m | Critical |
| **HttpTimeoutRateHigh** | >5% timeouts | 10m | Warning |
| **HttpConnectionPoolExhausted** | Pool full | 1m | Critical |
| **HighHttpLatency** | >2s latency | 10m | Warning |

**External Services**: OpenRouter, n8n, Qdrant, Unstructured, SmolDocling

---

## Metrics Reference

### Custom MeepleAI Metrics (Exposed by API)

#### RAG Metrics
```
meepleai_rag_questions_total              # Total questions processed
meepleai_rag_confidence_score             # Confidence histogram (0-1)
meepleai_rag_retrieval_latency_seconds    # Retrieval time
meepleai_rag_hallucination_detected       # Hallucination counter
meepleai_rag_precision_at_10              # P@10 metric
meepleai_rag_mrr                          # Mean Reciprocal Rank
meepleai_rag_citation_accuracy            # Citation validation %
```

#### PDF Processing Metrics
```
meepleai_pdf_processing_total{stage="1|2|3",status="success|failure"}
meepleai_pdf_quality_score               # Quality histogram (0-1)
meepleai_pdf_processing_duration_seconds # Processing time
meepleai_pdf_pages_processed_total       # Pages extracted
meepleai_pdf_backlog_size                # Queue size
```

#### Cache Metrics
```
meepleai_cache_hits_total                # Cache hits
meepleai_cache_misses_total              # Cache misses
meepleai_cache_hit_rate                  # Hit rate % (0-100)
meepleai_cache_latency_seconds           # Cache operation time
```

#### Authentication Metrics
```
meepleai_auth_login_total{status="success|failure|2fa_required"}
meepleai_auth_2fa_attempts_total{status="success|failure"}
meepleai_auth_api_key_usage_total
meepleai_auth_session_duration_seconds
```

#### LLM Cost Tracking
```
meepleai_llm_requests_total{provider,model}
meepleai_llm_tokens_total{provider,model,type="input|output"}
meepleai_llm_cost_usd{provider,model}
meepleai_llm_latency_seconds{provider,model}
```

See: [Prometheus LLM Queries](./prometheus-llm-queries.md) for query examples

### Standard Metrics (ASP.NET Core)

```
http_requests_total{method,endpoint,status_code}
http_request_duration_seconds{method,endpoint}
dotnet_collection_count_total{generation}
dotnet_total_memory_bytes
process_cpu_seconds_total
process_working_set_bytes
```

### Infrastructure Metrics (cAdvisor + node-exporter)

**cAdvisor** (container-level):
```
container_cpu_usage_seconds_total{name,id}
container_memory_usage_bytes{name,id}
container_network_receive_bytes_total{name,id}
container_fs_usage_bytes{name,id}
```

**node-exporter** (host-level):
```
node_cpu_seconds_total{mode}
node_memory_MemTotal_bytes
node_memory_MemAvailable_bytes
node_disk_read_bytes_total{device}
node_network_receive_bytes_total{device}
```

---

## AlertManager Integration

### Alert Routing Configuration

Configured in `../../alertmanager.yml`:

```yaml
route:
  receiver: 'default-email'
  group_by: ['alertname', 'severity']
  group_wait: 10s
  group_interval: 5m
  repeat_interval: 4h

  routes:
    - match:
        severity: critical
      receiver: 'pagerduty-oncall'
      continue: true

    - match_re:
        severity: ^(critical|warning)$
      receiver: 'slack-alerts'
      continue: true
```

### Notification Channels

| Receiver | Severity | Method | SLA |
|----------|----------|--------|-----|
| **pagerduty-oncall** | Critical | PagerDuty | 15 min |
| **slack-alerts** | Critical, Warning | Slack | 1 hour |
| **default-email** | All | Email | Best effort |

**Setup**: [Slack Notifications](./slack-notifications.md)

### Alert Severity Levels

#### Critical
- **Definition**: Immediate action required, user impact
- **Notifications**: Email + Slack + PagerDuty (24/7)
- **SLA Response**: 15 minutes
- **Examples**: API down, Database down, High hallucination rate, All PDF stages failed

#### Warning
- **Definition**: Problem that could become critical, action needed soon
- **Notifications**: Email + Slack
- **SLA Response**: 1 hour
- **Examples**: High latency, Low cache hit rate, High error rate (<5%), Low RAG confidence

#### Info
- **Definition**: Informational, no immediate action required
- **Notifications**: Email only
- **SLA Response**: Best effort
- **Examples**: High traffic (possible abuse), Stage 1 PDF failure (fallback works), High cache miss rate (warm-up)

---

## Grafana Integration

**Access**: http://localhost:3001 (admin/admin)

### Dashboards Using Prometheus Data

1. **api-performance.json** - Request rates, latency percentiles, error rates
2. **error-monitoring.json** - 5xx errors, stack traces, alert status
3. **cache-optimization.json** - Redis hit/miss rates, memory usage
4. **ai-quality-monitoring.json** - LLM confidence, hallucination detection
5. **ai-rag-operations.json** - Vector search, retrieval accuracy
6. **infrastructure.json** - CPU, memory, disk, network (Issue #705)
7. **quality-metrics-gauges.json** - P@10, MRR, recall scores
8. **http-retry-metrics.json** - Retry rates, circuit breaker status

**Setup**: [Grafana LLM Cost Dashboard](./grafana-llm-cost-dashboard.md)

### Data Source Configuration

Grafana automatically discovers Prometheus:

```yaml
# grafana-datasources.yml
datasources:
  - name: Prometheus
    type: prometheus
    access: proxy
    url: http://prometheus:9090
    isDefault: true
```

---

## Best Practices

### 1. Alert Design Principles

**DO**:
- ✅ Make alerts actionable (can be fixed by on-call)
- ✅ Include clear summary and description
- ✅ Link to runbooks for critical alerts
- ✅ Use appropriate severity levels
- ✅ Set `for` duration to avoid flapping
- ✅ Group related alerts by component

**DON'T**:
- ❌ Create alerts for informational metrics
- ❌ Set thresholds too sensitive (alert fatigue)
- ❌ Alert on symptoms without actionable fix
- ❌ Use generic names (ServiceDown)
- ❌ Forget to test alerts in staging

### 2. Naming Conventions

**Pattern**: `<Condition><Component><Metric>`

**Examples**:
- ✅ `HighApiErrorRate` (clear, specific)
- ✅ `PostgresDown` (component + condition)
- ✅ `VeryLowRagConfidence` (severity in name)
- ❌ `ServiceDown` (too generic)
- ❌ `Alert1` (not descriptive)

### 3. For Duration Guidelines

| Severity | Duration | Reasoning |
|----------|----------|-----------|
| **Critical** | 1-5 min | Fast response, genuine issue |
| **Warning** | 5-10 min | Avoid flapping, confirm trend |
| **Info** | 10-30 min | Trend confirmation |

### 4. Threshold Setting

**Process**:
1. Measure baseline over 7 days
2. Set threshold at 95th percentile + 20% margin
3. Monitor alert frequency in staging
4. Adjust based on false positive rate

**Example**:
```
Baseline P95 latency: 300ms
Initial threshold: 300ms * 1.2 = 360ms
After 1 week: Adjust to 400ms if too noisy
```

### 5. Runbook Requirements

**Every Critical Alert MUST Have**:
- Diagnosis steps
- Fix procedures
- Escalation path
- Related metrics to check

**Location**: `docs/05-operations/runbooks/<alert-name>.md`

**Template**: [Runbook Template](../runbooks/templates/runbook-template.md)

---

## Testing Alerts

### 1. Validate Syntax

```bash
# Check all alert files
docker compose exec prometheus promtool check rules /etc/prometheus/alerts/*.yml

# Check specific file
docker compose exec prometheus promtool check rules /etc/prometheus/alerts/api-performance.yml
```

### 2. Test Alert Expression

**Prometheus UI** (http://localhost:9090/graph):
```
# Paste alert expression and verify results
rate(http_requests_total{status=~"5.."}[5m]) > 0.01

# Should return data when condition is met
```

### 3. Trigger Alert Manually (Staging)

**Example: Trigger HighApiErrorRate**

```bash
# 1. Temporarily lower threshold in staging
expr: rate(http_requests_total{status=~"5.."}[5m]) > 0.00001  # Very low

# 2. Generate some 5xx errors
curl http://staging-api/nonexistent-endpoint

# 3. Wait 5 minutes

# 4. Verify alert
# - Prometheus UI → Alerts (should show "Firing")
# - AlertManager UI → Alerts (should appear)
# - Check email/Slack notifications

# 5. Restore original threshold
expr: rate(http_requests_total{status=~"5.."}[5m]) > 0.01
```

### 4. End-to-End Notification Test

**Steps**:
1. Trigger alert in staging
2. Verify Prometheus shows "Firing" (http://localhost:9090/alerts)
3. Verify AlertManager receives alert (http://localhost:9093)
4. Check email received
5. Check Slack message received
6. (Skip PagerDuty in staging)

**Validation**:
```bash
# Check AlertManager status
curl http://localhost:9093/api/v1/alerts | jq

# Check Prometheus alert state
curl http://localhost:9090/api/v1/alerts | jq
```

---

## Troubleshooting

### Alert Never Triggers

**Diagnosis**:

1. **Check metric exists**:
   ```bash
   curl 'http://localhost:9090/api/v1/query?query=<metric_name>'
   ```

2. **Validate rule syntax**:
   ```bash
   docker compose exec prometheus promtool check rules /etc/prometheus/alerts/my-alert.yml
   ```

3. **Check evaluation**:
   - Prometheus UI → Alerts
   - Look for "State": Inactive, Pending, Firing

4. **Review `for` duration**:
   - May be too long, condition doesn't persist
   - Try reducing for testing

5. **Check threshold**:
   - May be too strict, never met
   - Review baseline metrics

### Alert Fires Too Frequently (Flapping)

**Fixes**:

1. **Increase `for` duration**:
   ```yaml
   for: 5m  # Change to 10m
   ```

2. **Add hysteresis**:
   ```yaml
   # Alert fires at >80%, resolves at <70%
   expr: metric > 0.80 or (metric > 0.70 and ALERTS{alertname="MyAlert"})
   ```

3. **Use time averaging**:
   ```yaml
   # Instead of instant value
   expr: avg_over_time(metric[5m]) > threshold
   ```

### Alert Not Notified

**Diagnosis**:

1. **Check AlertManager receives alert**:
   - http://localhost:9093
   - Should see alert in list

2. **Check routing configuration**:
   ```bash
   docker compose exec alertmanager amtool config routes
   ```

3. **Verify receiver configured**:
   - Email: SMTP settings correct
   - Slack: Webhook URL valid
   - PagerDuty: API key configured

4. **Check AlertManager logs**:
   ```bash
   docker compose logs alertmanager | grep ERROR
   ```

### Metrics Not Collected

**Diagnosis**:

1. **Check target is UP**:
   - Prometheus UI → Status → Targets
   - Should be green/UP

2. **Verify scrape config**:
   ```yaml
   scrape_configs:
     - job_name: 'api'
       static_configs:
         - targets: ['api:8080']  # Correct service name?
   ```

3. **Check firewall/network**:
   ```bash
   # From Prometheus container
   docker compose exec prometheus wget -O- http://api:8080/metrics
   ```

4. **Verify metrics endpoint**:
   ```bash
   curl http://localhost:8080/metrics
   # Should return Prometheus format metrics
   ```

### High Prometheus Memory Usage

**Causes**: High cardinality labels, long retention

**Fixes**:

1. **Reduce retention**:
   ```yaml
   command:
     - '--storage.tsdb.retention.time=7d'  # Instead of 90d
   ```

2. **Limit memory**:
   ```yaml
   deploy:
     resources:
       limits:
         memory: 2GB
   ```

3. **Reduce cardinality**:
   - Avoid labels with many unique values
   - Use recording rules to pre-aggregate
   - Drop unnecessary labels

**Example Recording Rule**:
```yaml
# Pre-aggregate high-cardinality metric
groups:
  - name: recording_rules
    interval: 1m
    rules:
      - record: api:requests:rate5m
        expr: rate(http_requests_total[5m])
```

---

## Adding New Alerts

### Step-by-Step Process

1. **Create or update alert file** in `infra/prometheus/alerts/`:
   ```yaml
   groups:
     - name: my_feature_alerts
       interval: 30s
       rules:
         - alert: MyFeatureAlert
           expr: my_metric > threshold
           for: 5m
           labels:
             severity: warning
             component: feature-name
             team: backend
           annotations:
             summary: "Short description (1 line)"
             description: |
               Detailed description with context.
               Current value: {{ $value }}
               Threshold: {{ $labels.threshold }}
             runbook_url: "https://docs.meepleai.dev/runbooks/my-feature-alert"
   ```

2. **Prometheus auto-reloads** rules every 15s (no restart needed)

3. **Verify in Prometheus UI**:
   - Alerts: http://localhost:9090/alerts
   - Rules: http://localhost:9090/rules

4. **Add routing** in `alertmanager.yml` if custom notification needed

5. **Create runbook** if severity is Critical:
   - Location: `docs/05-operations/runbooks/my-feature-alert.md`
   - Template: [Runbook Template](../runbooks/templates/runbook-template.md)

6. **Test in staging** before production

---

## Query Examples

### API Performance

```promql
# Request rate (req/s)
rate(http_requests_total[5m])

# Error rate (%)
rate(http_requests_total{status=~"5.."}[5m]) / rate(http_requests_total[5m]) * 100

# P95 latency
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))

# Success rate (%)
rate(http_requests_total{status=~"2.."}[5m]) / rate(http_requests_total[5m]) * 100
```

### RAG Quality

```promql
# Average confidence score
avg(meepleai_rag_confidence_score)

# Hallucination rate (%)
rate(meepleai_rag_hallucination_detected[1h]) / rate(meepleai_rag_questions_total[1h]) * 100

# P@10 metric
avg(meepleai_rag_precision_at_10)

# Retrieval latency P95
histogram_quantile(0.95, rate(meepleai_rag_retrieval_latency_seconds_bucket[5m]))
```

### Cache Performance

```promql
# Cache hit rate (%)
meepleai_cache_hit_rate

# Alternative calculation
rate(meepleai_cache_hits_total[5m]) / (rate(meepleai_cache_hits_total[5m]) + rate(meepleai_cache_misses_total[5m])) * 100
```

### Database Health

```promql
# Connection pool usage (%)
pg_stat_database_numbackends / pg_settings_max_connections * 100

# Average query time
rate(pg_stat_database_tup_fetched[5m]) / rate(pg_stat_database_xact_commit[5m])
```

### PDF Processing

```promql
# Success rate by stage
rate(meepleai_pdf_processing_total{status="success"}[1h]) / rate(meepleai_pdf_processing_total[1h]) * 100

# Average quality score
avg(meepleai_pdf_quality_score)

# Processing time P95
histogram_quantile(0.95, rate(meepleai_pdf_processing_duration_seconds_bucket[5m]))
```

See: [Prometheus LLM Queries](./prometheus-llm-queries.md) for more examples

---

## Monitoring Alert Health

### Prometheus UI

**Alerts Page** (http://localhost:9090/alerts):
- View alert state: Inactive, Pending, Firing
- See evaluation timestamp
- Check labels and annotations

**Rules Page** (http://localhost:9090/rules):
- All loaded rules
- Evaluation interval
- Last evaluation time

### AlertManager UI

**URL**: http://localhost:9093

**Features**:
- Active alerts
- Silences (temporary mute)
- Routing tree visualization
- Status page

### Grafana Dashboard

**error-monitoring.json** provides:
- Active alerts count
- Alert firing frequency
- Mean time to resolution (MTTR)
- Alert trends over time

---

## Related Documentation

### Setup & Configuration
- **[Infrastructure Overview](../infrastructure-overview.md)** - Complete infra guide
- **[Monitoring Strategy](./monitoring-strategy.md)** - Overall monitoring approach
- **[AlertManager Configuration](../../infra/alertmanager.yml)** - Alert routing
- **[Grafana Setup](./grafana-llm-cost-dashboard.md)** - Dashboard configuration

### Query References
- **[Prometheus LLM Queries](./prometheus-llm-queries.md)** - LLM cost tracking queries
- **[Custom Metrics API](../../03-api/metrics-api.md)** - API metrics reference

### Runbooks (Incident Response)
- **[Runbooks Index](../runbooks/README.md)** - All operational runbooks
- **[High Error Rate](../runbooks/high-error-rate.md)** - API error spike response
- **[Slow Performance](../runbooks/slow-performance.md)** - Performance degradation
- **[AI Quality Low](../runbooks/ai-quality-low.md)** - RAG quality issues
- **[Infrastructure Monitoring](../runbooks/infrastructure-monitoring.md)** - Resource alerts (Issue #705)
- **[Dependency Down](../runbooks/dependency-down.md)** - External service failures

### Operations
- **[Deployment Guide](../deployment-guide.md)** - Production deployment
- **[Disaster Recovery](../deployment/disaster-recovery.md)** - DR procedures
- **[Backup Strategy](../backup/backup-strategy.md)** - Data backup automation

### Component-Specific
- **[Alert Rules Directory](../../infra/prometheus/alerts/)** - Individual alert files
- **[Dashboard Directory](../../infra/dashboards/)** - Grafana dashboard JSON files

---

## Changelog

### 2025-12-08: Documentation Consolidation

**Changes**:
- ✅ Consolidated `infra/prometheus/README.md` + `infra/prometheus/alerts/README.md`
- ✅ Added infrastructure monitoring alerts documentation (Issue #705)
- ✅ Added complete metrics reference section
- ✅ Added testing and troubleshooting comprehensive guides
- ✅ Moved to `docs/05-operations/monitoring/prometheus-setup.md`
- ✅ Updated all cross-references to new docs structure

---

**Version**: 2.0 (Post-Consolidation)
**Last Updated**: 2025-12-08
**Maintainer**: Operations Team
**Alert Rules**: 40+ across 9 categories
**Metrics**: 50+ custom + standard ASP.NET Core + infrastructure
