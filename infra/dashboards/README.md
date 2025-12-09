# Grafana Dashboards

This directory contains Grafana dashboard JSON files for the MeepleAI observability stack (OPS-02).

## Dashboard Files

✅ **Implemented Dashboards** (13 total):

1. **api-performance.json** - API performance metrics
   - Request rate (req/s)
   - Request duration (p50, p95, p99)
   - Error rate (5xx)
   - Status code distribution
   - Active requests gauge

2. **ai-rag-operations.json** - AI/RAG operations metrics
   - RAG request rate by game
   - RAG request duration percentiles
   - Token usage over time
   - Confidence score distribution
   - RAG errors by type
   - Vector search latency

3. **infrastructure.json** - Infrastructure and runtime metrics
   - GC heap size by generation
   - GC collections/sec
   - Thread pool size
   - Exception rate
   - Cache hit rate gauge
   - Cache operations (hits, misses, evictions)
   - PDF processing metrics

4. **error-monitoring.json** - Error monitoring and alerting
   - Error rates and spikes
   - Error type distribution
   - 5xx status codes

5. **cache-optimization.json** - Cache performance metrics
   - Redis cache hit/miss rates
   - Cache memory usage
   - Eviction rates

6. **ai-quality-monitoring.json** - AI quality metrics
   - LLM response quality scores
   - Hallucination detection rates
   - Confidence scores

7. **quality-metrics-gauges.json** - System quality indicators
   - Overall system quality gauges
   - SLA compliance metrics

8. **http-retry-metrics.json** - HTTP retry monitoring
   - HTTP retry patterns and success rates
   - Retry backoff effectiveness
   - Failed retry tracking

9. **infrastructure-monitoring.json** - Infrastructure monitoring (Issue #705)
   - Container resource usage (cAdvisor)
   - Host system metrics (node-exporter)
   - CPU, memory, disk, network statistics

10. **ingestion-services.json** - Data ingestion monitoring
    - PDF processing pipeline metrics
    - Document ingestion rates
    - Processing queue status

11. **llm-cost-monitoring.json** - LLM cost tracking
    - Token usage by provider and model
    - Cost per request
    - Budget tracking and alerts

12. **rag-evaluation.json** - RAG evaluation metrics (ADR-016)
    - Recall@5, Recall@10, nDCG@10, MRR
    - Grid search results
    - Configuration performance comparison

13. **2fa-security-monitoring.json** - 2FA security metrics (Issue #576)
    - 2FA adoption rate
    - Authentication success/failure rates
    - TOTP validation metrics
    - Backup code usage

## Auto-Provisioning

Dashboards in this directory are automatically provisioned to Grafana on startup via the `grafana-dashboards.yml` configuration.

## Usage

1. Place JSON dashboard files in this directory
2. Restart Grafana service: `docker compose restart grafana`
3. Dashboards will appear in Grafana UI under the "MeepleAI" folder

## Creating Dashboards

You can create dashboards in two ways:

1. **Via Grafana UI**: Create dashboard in UI, then export as JSON and save here
2. **Via JSON**: Write dashboard JSON manually (see Grafana docs)

## Links

- Grafana Dashboard API: https://grafana.com/docs/grafana/latest/developers/http_api/dashboard/
- Dashboard JSON Schema: https://grafana.com/docs/grafana/latest/dashboards/json-model/
