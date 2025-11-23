# Prometheus Configuration

## Contenuto

Configurazione Prometheus per monitoring e alerting del sistema MeepleAI.

## Struttura

```
prometheus/
└── alerts/                    Alert rules modular (40+ rules)
    ├── api-performance.yml    API errors, latency, throughput
    ├── cache-performance.yml  Redis cache hit rate, latency
    ├── database-health.yml    PostgreSQL connection, query time
    ├── infrastructure.yml     CPU, memory, disk usage
    ├── vector-search.yml      Qdrant performance
    ├── quality-metrics.yml    RAG confidence, hallucination detection
    ├── pdf-processing.yml     PDF pipeline alerts
    ├── prompt-management.yml  Prompt versioning alerts
    └── http-retry-alerts.yaml HTTP client retry patterns
```

## File Principali

### ../prometheus.yml

Configurazione principale Prometheus:
- **Scrape Interval**: 15s
- **Evaluation Interval**: 15s
- **Targets**:
  - API: `api:8080/metrics`
  - Prometheus self: `localhost:9090`
  - Node Exporter (se abilitato)
- **Alert Rules**: Include tutte le rules da `alerts/*.yml`

### ../prometheus-rules.yml

Alert rules consolidate (deprecato, usare `alerts/` invece).

## Alert Rules (alerts/)

### Categoria: API Performance (api-performance.yml)

**Rules**:
- `HighApiErrorRate`: Error rate 5xx >1% per 5min
- `HighApiLatency`: P95 latency >1s per 10min
- `ApiDown`: API non raggiungibile per 1min
- `HighRequestRate`: Request rate >1000 req/s (possibile abuse)
- `LowSuccessRate`: Success rate <95% per 5min

**Severity**: Critical, Warning, Info

### Categoria: Cache Performance (cache-performance.yml)

**Rules**:
- `LowCacheHitRate`: Hit rate <80% per 10min
- `HighCacheMissRate`: Miss rate >20% per 5min
- `RedisDown`: Redis non raggiungibile

**Target**: >85% cache hit rate

### Categoria: Database Health (database-health.yml)

**Rules**:
- `HighDatabaseConnections`: Connection pool >80% per 5min
- `SlowDatabaseQueries`: Query time >500ms per 10min
- `PostgresDown`: PostgreSQL non raggiungibile

**Metriche**: Connection pool, query duration, transaction rate

### Categoria: Infrastructure (infrastructure.yml)

**Rules**:
- `HighCpuUsage`: CPU >80% per 10min
- `HighMemoryUsage`: Memory >85% per 10min
- `LowDiskSpace`: Disk <10% free

**Applicabile a**: Tutti i container

### Categoria: Vector Search (vector-search.yml)

**Rules**:
- `HighQdrantLatency`: Query latency >500ms
- `QdrantDown`: Qdrant non raggiungibile

**Metriche**: Query latency, indexing rate, collection size

### Categoria: Quality Metrics (quality-metrics.yml)

**Rules**:
- `LowRagConfidence`: Confidence media <0.70 per 1h
- `HighHallucinationRate`: Hallucination detection >3% per 30min
- `LowRetrievalPrecision`: P@10 <0.85
- `LowRetrievalMRR`: MRR <0.75

**Target**: Confidence ≥0.70, Hallucination <3%

### Categoria: PDF Processing (pdf-processing.yml)

**Rules**:
- `HighPdfProcessingFailureRate`: Failure rate >5% per 1h
- `LowPdfQualityScore`: Quality score media <0.80
- `SlowPdfProcessing`: Processing time >30s
- `AllExtractionStagesFailed`: Tutti e 3 gli stage falliti

**Pipeline**: 3-stage fallback (Unstructured → SmolDocling → Docnet)

### Categoria: Prompt Management (prompt-management.yml)

**Rules**:
- `OutdatedPromptTemplate`: Template version outdated
- `HighPromptTokenUsage`: Token usage >90% del limite

### Categoria: HTTP Retry (http-retry-alerts.yaml)

**Rules**:
- `HighHttpRetryRate`: Retry rate >10%
- `HttpCircuitBreakerOpen`: Circuit breaker aperto

## Configurazione Alert Manager

Vedi `../alertmanager.yml` per routing:
- **Email**: Tutte le severity
- **Slack**: Warning, Critical
- **PagerDuty**: Solo Critical (24/7 on-call)

## Accesso Prometheus UI

```bash
# Locale
open http://localhost:9090

# Query esempi
rate(http_requests_total[5m])
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))
meepleai_rag_confidence_score
```

## Grafana Integration

Dashboard Grafana utilizzano queste metrics:
- `dashboards/api-performance.json`
- `dashboards/ai-quality-monitoring.json`
- `dashboards/infrastructure.json`
- `dashboards/error-monitoring.json`

Accedi a Grafana: http://localhost:3001

## Metriche Custom (API)

L'API .NET espone metriche custom:

### RAG Metrics
- `meepleai_rag_questions_total`
- `meepleai_rag_confidence_score` (histogram)
- `meepleai_rag_retrieval_latency_seconds`

### PDF Processing
- `meepleai_pdf_processing_total{stage="1|2|3",status="success|failure"}`
- `meepleai_pdf_quality_score` (histogram)
- `meepleai_pdf_processing_duration_seconds`

### Cache
- `meepleai_cache_hits_total`
- `meepleai_cache_misses_total`
- `meepleai_cache_hit_rate`

### Authentication
- `meepleai_auth_login_total{status="success|failure|2fa_required"}`
- `meepleai_auth_api_key_usage_total`

## Aggiungere Nuovi Alert

1. Crea file in `alerts/` o aggiungi a file esistente:
   ```yaml
   groups:
     - name: my_feature
       rules:
         - alert: MyFeatureAlert
           expr: my_metric > 100
           for: 5m
           labels:
             severity: warning
           annotations:
             summary: "Feature alert triggered"
             description: "Metric exceeded threshold"
   ```

2. Prometheus ricarica automaticamente le rules ogni 15s

3. Verifica in Prometheus UI:
   - Alerts: http://localhost:9090/alerts
   - Rules: http://localhost:9090/rules

4. Aggiungi routing in `../alertmanager.yml` se necessario

## Testing Alert Rules

```bash
# Valida sintassi
docker compose exec prometheus promtool check rules /etc/prometheus/alerts/*.yml

# Test query
curl 'http://localhost:9090/api/v1/query?query=up'

# Trigger manuale (for testing)
# Modifica threshold temporaneamente e genera carico
```

## Retention & Storage

- **Default Retention**: 15 giorni
- **Staging Retention**: 60 giorni (`compose.staging.yml`)
- **Production Retention**: 90 giorni, max 50GB (`compose.prod.yml`)

Configurazione:
```yaml
command:
  - '--storage.tsdb.retention.time=90d'
  - '--storage.tsdb.retention.size=50GB'
```

## Troubleshooting

### Alert Non Triggera

1. Verifica metrica esiste:
   ```bash
   curl 'http://localhost:9090/api/v1/query?query=<metric_name>'
   ```

2. Controlla sintassi rule:
   ```bash
   docker compose exec prometheus promtool check rules /etc/prometheus/alerts/my-alert.yml
   ```

3. Controlla evaluation:
   - UI → Alerts → Vedi "State" (Pending/Firing/Inactive)

### Metrics Non Raccolte

1. Verifica target è up:
   - UI → Status → Targets
   - Deve essere "UP" (verde)

2. Controlla scrape config in `prometheus.yml`:
   ```yaml
   scrape_configs:
     - job_name: 'api'
       static_configs:
         - targets: ['api:8080']
   ```

3. Verifica firewall/network

### High Memory Usage

Prometheus può usare molta memoria con alta cardinalità:

**Fix**:
- Riduci retention: `--storage.tsdb.retention.time=7d`
- Limita memory: `deploy.resources.limits.memory: 2GB`
- Riduci cardinality: Evita label con troppi valori unici

## Best Practices

1. **Alert Fatigue**: Non creare troppi alert, solo actionable
2. **Severity Appropriata**: Critical solo se richiede azione immediata
3. **Runbooks**: Ogni alert critico dovrebbe avere runbook (docs/05-operations/runbooks/)
4. **Testing**: Testa alert in staging prima di produzione
5. **Labels Consistenti**: Usa label standard (severity, component, team)

## Related Documentation

- `../alertmanager.yml` - Alert routing configuration
- `../dashboards/README.md` - Grafana dashboards
- `docs/05-operations/monitoring/prometheus-setup.md`
- `docs/05-operations/runbooks/` - Incident response guides
