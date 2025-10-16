# Grafana Dashboards

This directory contains Grafana dashboard JSON files for the MeepleAI observability stack (OPS-02).

## Dashboard Files

✅ **Implemented Dashboards**:

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
