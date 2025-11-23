# Prometheus Alert Rules

## Contenuto

Alert rules modulari per monitoring completo del sistema MeepleAI (40+ rules).

## File Alert

### api-performance.yml (7 rules)

**Metriche**: HTTP requests, latency, error rate, throughput

**Alert**:
- `HighApiErrorRate`: 5xx error rate >1% per 5min (Critical)
- `HighApiLatency`: P95 latency >1s per 10min (Warning)
- `VeryHighApiLatency`: P99 latency >3s per 5min (Critical)
- `ApiDown`: API non risponde per 1min (Critical)
- `HighRequestRate`: >1000 req/s (possibile abuse) (Warning)
- `LowSuccessRate`: Success rate <95% per 5min (Warning)
- `ApiMemoryLeakSuspected`: Memory usage crescente costante (Critical)

**Threshold**:
- Error rate target: <1%
- P95 latency target: <500ms
- Availability target: 99.9%

### cache-performance.yml (3 rules)

**Metriche**: Redis cache hit rate, miss rate, latency

**Alert**:
- `LowCacheHitRate`: Hit rate <80% per 10min (Warning)
- `HighCacheMissRate`: Miss rate >20% per 5min (Info)
- `RedisDown`: Redis non raggiungibile per 1min (Critical)

**Threshold**:
- Hit rate target: >85%
- Latency target: <10ms

### database-health.yml (4 rules)

**Metriche**: PostgreSQL connections, query time, transaction rate

**Alert**:
- `HighDatabaseConnections`: Connection pool usage >80% per 5min (Warning)
- `SlowDatabaseQueries`: Average query time >500ms per 10min (Warning)
- `PostgresDown`: PostgreSQL non raggiungibile per 1min (Critical)
- `HighDatabaseCPU`: CPU usage >70% per 10min (Warning)

**Threshold**:
- Query time target: <100ms (p50), <500ms (p95)
- Connection pool: Max 100 connections

### infrastructure.yml (5 rules)

**Metriche**: CPU, memory, disk, network

**Alert**:
- `HighCpuUsage`: CPU >80% per 10min (Warning)
- `VeryHighCpuUsage`: CPU >95% per 5min (Critical)
- `HighMemoryUsage`: Memory >85% per 10min (Warning)
- `LowDiskSpace`: Disk free <10% (Critical)
- `HighNetworkTraffic`: Network traffic >1GB/s (Info)

**Applicabile a**: Tutti i container (api, web, postgres, qdrant, redis)

### vector-search.yml (3 rules)

**Metriche**: Qdrant query latency, indexing rate, collection size

**Alert**:
- `HighQdrantLatency`: Query latency >500ms per 5min (Warning)
- `QdrantDown`: Qdrant non raggiungibile per 1min (Critical)
- `QdrantIndexingBacklog`: Indexing backlog >1000 documents (Warning)

**Threshold**:
- Query latency target: <200ms (p95)
- Indexing rate: >100 docs/sec

### quality-metrics.yml (8 rules)

**Metriche**: RAG confidence, hallucination rate, retrieval metrics

**Alert**:
- `LowRagConfidence`: Confidence media <0.70 per 1h (Warning)
- `VeryLowRagConfidence`: Confidence media <0.60 per 30min (Critical)
- `HighHallucinationRate`: Hallucination >3% per 30min (Critical)
- `LowRetrievalPrecision`: P@10 <0.85 per 1h (Warning)
- `LowRetrievalMRR`: MRR <0.75 per 1h (Warning)
- `HighRetrievalLatency`: Retrieval time >1s (Warning)
- `LowCitationAccuracy`: Citation accuracy <95% (Critical)
- `HighLLMTokenUsage`: Token usage vicino al limite (Warning)

**Threshold**:
- Confidence target: ≥0.70
- Hallucination target: <3%
- P@10 target: ≥0.85
- MRR target: ≥0.75

### pdf-processing.yml (7 rules)

**Metriche**: PDF pipeline success rate, quality score, processing time

**Alert**:
- `HighPdfProcessingFailureRate`: Failure rate >5% per 1h (Warning)
- `AllPdfExtractionStagesFailed`: Tutti e 3 gli stage falliti (Critical)
- `LowPdfQualityScore`: Quality score media <0.80 per 1h (Warning)
- `VeryLowPdfQualityScore`: Quality score <0.60 (Critical)
- `SlowPdfProcessing`: Processing time >30s per document (Warning)
- `HighStage1FailureRate`: Unstructured failure >20% (Info)
- `PdfProcessingBacklog`: Backlog >10 documents (Warning)

**Pipeline**: 3-stage fallback
- Stage 1 (Unstructured): Target 80% success, 1.3s avg
- Stage 2 (SmolDocling): Target 15% fallback, 3-5s avg
- Stage 3 (Docnet): 5% fallback, <1s avg

**Threshold**:
- Overall success rate: >95%
- Quality score: ≥0.80

### prompt-management.yml (3 rules)

**Metriche**: Prompt template versions, token usage

**Alert**:
- `OutdatedPromptTemplate`: Template version >30 giorni (Warning)
- `HighPromptTokenUsage`: Token usage >90% del limite (Warning)
- `PromptTemplateNotFound`: Template richiesto non trovato (Critical)

**Best Practice**: Aggiorna template ogni 2-4 settimane

### http-retry-alerts.yaml (5 rules)

**Metriche**: HTTP client retry patterns, circuit breaker status

**Alert**:
- `HighHttpRetryRate`: Retry rate >10% per 10min (Warning)
- `HttpCircuitBreakerOpen`: Circuit breaker aperto (Critical)
- `HttpTimeoutRateHigh`: Timeout rate >5% (Warning)
- `HttpConnectionPoolExhausted`: Connection pool esaurito (Critical)
- `HighHttpLatency`: HTTP call latency >2s (Warning)

**External Services**: OpenRouter, n8n, Qdrant, Unstructured, SmolDocling

## Alert Severity Levels

### Critical
- **Definizione**: Richiede azione immediata, impatto utente critico
- **Notifiche**: Email, Slack, PagerDuty (24/7 on-call)
- **SLA Response**: 15 minuti
- **Esempi**:
  - API down
  - Database down
  - High hallucination rate
  - All PDF extraction stages failed

### Warning
- **Definizione**: Problema che potrebbe diventare critico, action needed soon
- **Notifiche**: Email, Slack
- **SLA Response**: 1 ora
- **Esempi**:
  - High latency
  - Low cache hit rate
  - High error rate (ma <5%)
  - Low RAG confidence

### Info
- **Definizione**: Informational, no immediate action required
- **Notifiche**: Email only
- **SLA Response**: Best effort
- **Esempi**:
  - High traffic (possibile abuse)
  - Stage 1 PDF failure (fallback funziona)
  - High cache miss rate (warm-up period)

## Alert Routing (AlertManager)

Configurato in `../../alertmanager.yml`:

```yaml
route:
  receiver: 'default-email'
  routes:
    - match:
        severity: critical
      receiver: 'pagerduty-oncall'
      continue: true
    - match:
        severity: critical|warning
      receiver: 'slack-alerts'
      continue: true
```

**Receivers**:
- `default-email`: Tutte le severity
- `slack-alerts`: Warning + Critical
- `pagerduty-oncall`: Solo Critical

## Template Alert

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

## Best Practices

### 1. Naming Conventions
- PascalCase: `HighApiErrorRate`
- Descrittivo: Indica cosa è rotto
- Evitare generici: `ServiceDown` → `ApiDown`, `PostgresDown`

### 2. Severity Assignment
- **Critical**: Broken functionality, data loss, security breach
- **Warning**: Degraded performance, approaching limits
- **Info**: FYI, trends, non-actionable

### 3. For Duration
- **Critical**: 1-5min (azione rapida)
- **Warning**: 5-10min (evita flapping)
- **Info**: 10-30min (trend confirmation)

### 4. Annotations
- **Summary**: 1-line description
- **Description**: Include:
  - Cosa è rotto
  - Valore attuale vs threshold
  - Possibile impatto utente
  - Link runbook (se critico)

### 5. Runbooks
Ogni alert critico dovrebbe avere runbook:
- Location: `docs/05-operations/runbooks/<alert-name>.md`
- Include:
  - Diagnosis steps
  - Fix procedures
  - Escalation path

## Testing Alert

### 1. Validate Syntax
```bash
docker compose exec prometheus promtool check rules /etc/prometheus/alerts/*.yml
```

### 2. Test Query
```bash
# Query expression in Prometheus UI
http://localhost:9090/graph
# Inserisci expr dell'alert e verifica risultati
```

### 3. Trigger Manuale (Staging)
```bash
# Esempio: Trigger HighApiErrorRate
# 1. Modifica threshold temporaneamente a 0.001% (0.00001)
# 2. Aspetta 5min
# 3. Verifica alert in Prometheus UI e AlertManager
# 4. Ripristina threshold originale
```

### 4. End-to-End Test
```bash
# Verifica notifiche:
# 1. Trigger alert in staging
# 2. Controlla email ricevuta
# 3. Controlla messaggio Slack
# 4. (Skip PagerDuty in staging)
```

## Monitoring Alert Health

### Prometheus UI
- **Alerts**: http://localhost:9090/alerts
  - Vedi stato: Inactive, Pending, Firing
- **Rules**: http://localhost:9090/rules
  - Vedi tutte le rules e valutazione

### AlertManager UI
- **URL**: http://localhost:9093
- **Vedi**:
  - Alerts attivi
  - Silences configurati
  - Routing tree

### Grafana Dashboard
Dashboard dedicato: `error-monitoring.json`
- Active alerts count
- Alert firing frequency
- Mean time to resolution

## Troubleshooting

### Alert Non Triggera Mai

**Possibili Cause**:
1. Metrica non esiste: Controlla `curl http://localhost:9090/api/v1/query?query=<metric>`
2. Threshold troppo alto: Rivedi threshold
3. `for` duration troppo lunga: Riduci per test
4. Sintassi errata: `promtool check rules`

### Alert Troppo Frequente (Flapping)

**Fix**:
1. Aumenta `for` duration (es. 5m → 10m)
2. Aggiungi isteresi: Alert at >80%, resolve at <70%
3. Usa `avg_over_time()` invece di valori istantanei

### Alert Non Viene Notificato

**Check**:
1. AlertManager riceve alert: http://localhost:9093
2. Routing config corretta: Vedi `alertmanager.yml`
3. Receiver configurato: Email SMTP, Slack webhook, PagerDuty API key

## Related Documentation

- `../../alertmanager.yml` - Alert routing
- `../../prometheus.yml` - Prometheus config
- `../README.md` - Prometheus overview
- `docs/05-operations/monitoring/alerting-strategy.md`
- `docs/05-operations/runbooks/` - Incident response
