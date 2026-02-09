# Grafana Dashboard Import Guide

**Purpose**: Import pre-configured dashboards for MeepleAI monitoring
**Created**: 2026-02-09 (Issue #3909, #3955 validation)

---

## Available Dashboards

**Location**: `infra/monitoring/grafana/dashboards/`

1. **multi-tier-cache-performance.json** - Cache performance monitoring (Issue #3909)
2. **k6-load-testing.json** - Load testing metrics
3. **shared-catalog-performance.json** - Catalog performance

---

## Grafana Access

**URL**: http://localhost:3001
**Credentials**: See `infra/secrets/grafana.secret` or default (admin/admin)

---

## Import Dashboard Instructions

### Method 1: UI Import (Recommended)

1. **Open Grafana**:
   ```
   http://localhost:3001
   ```

2. **Navigate to Dashboards**:
   - Click "Dashboards" in left sidebar (four squares icon)
   - Click "New" → "Import"

3. **Upload JSON**:
   - Click "Upload dashboard JSON file"
   - Select: `infra/monitoring/grafana/dashboards/multi-tier-cache-performance.json`
   - Click "Load"

4. **Configure Data Source**:
   - Prometheus: Select "Prometheus" (should be default datasource)
   - Click "Import"

5. **Verify Dashboard**:
   - Dashboard should load with panels
   - Verify data is flowing (metrics visible)

6. **Repeat for other dashboards** (k6-load-testing.json, shared-catalog-performance.json)

---

### Method 2: Automated Import (Docker Compose)

**Configuration**: Update `docker-compose.yml` to auto-provision dashboards

```yaml
# infra/docker-compose.yml
services:
  grafana:
    # ... existing config ...
    volumes:
      - ./monitoring/grafana/provisioning:/etc/grafana/provisioning
      - ./monitoring/grafana/dashboards:/var/lib/grafana/dashboards
    environment:
      - GF_DASHBOARDS_DEFAULT_HOME_DASHBOARD_PATH=/var/lib/grafana/dashboards/multi-tier-cache-performance.json
```

**Create provisioning config**: `infra/monitoring/grafana/provisioning/dashboards/default.yml`

```yaml
apiVersion: 1

providers:
  - name: 'MeepleAI Dashboards'
    orgId: 1
    folder: 'MeepleAI'
    type: file
    disableDeletion: false
    updateIntervalSeconds: 10
    allowUiUpdates: true
    options:
      path: /var/lib/grafana/dashboards
      foldersFromFilesStructure: false
```

**Restart Grafana**:
```bash
cd infra
docker compose restart grafana
```

---

## Dashboard Details

### Multi-Tier Cache Performance Dashboard

**Metrics Monitored**:
- Cache hit rate (target: > 80%)
- Cache hits/misses total
- Cache evictions
- Cache promotions (L2 → L1)
- TTL adjustments
- Cache latency (P95)
- Dashboard-specific cache invalidations

**Queries**:
```promql
# Cache Hit Rate
sum(rate(meepleai_cache_hits_total[5m])) /
(sum(rate(meepleai_cache_hits_total[5m])) + sum(rate(meepleai_cache_misses_total[5m]))) * 100

# Cache Invalidations (Dashboard specific)
rate(meepleai_cache_dashboard_invalidations_total[5m])

# Cache Latency P95
histogram_quantile(0.95, rate(meepleai_cache_operation_latency_bucket[5m]))
```

**Thresholds**:
- Hit rate: < 70% (red), 70-80% (yellow), > 80% (green)
- Latency P95: < 10ms (green), 10-50ms (yellow), > 50ms (red)

---

### K6 Load Testing Dashboard

**Purpose**: Monitor load test results from k6 performance testing

**Metrics**:
- HTTP request rate
- HTTP request duration (P95, P99)
- Error rate
- Virtual users (VUs)

---

### Shared Catalog Performance Dashboard

**Purpose**: Monitor shared catalog FTS performance

**Metrics**:
- Full-text search latency
- Query count
- Result count distribution

---

## Verification

### Check Metrics Endpoint

```bash
# Verify Prometheus metrics exposed
curl http://localhost:8080/metrics

# Expected output (sample):
# HELP meepleai_cache_hits_total Total number of cache hits
# TYPE meepleai_cache_hits_total counter
# meepleai_cache_hits_total{cache_type="redis",operation="get"} 1234

# HELP meepleai_cache_dashboard_invalidations_total Dashboard cache invalidations
# TYPE meepleai_cache_dashboard_invalidations_total counter
# meepleai_cache_dashboard_invalidations_total 42
```

---

### Check Prometheus Scraping

**URL**: http://localhost:9090 (if Prometheus running)

1. **Verify Target**:
   - Navigate to Status → Targets
   - Should see "meepleai-api" target
   - State: UP

2. **Query Metrics**:
   - Navigate to Graph
   - Query: `meepleai_cache_hits_total`
   - Should return data

---

### Check Grafana Visualization

**URL**: http://localhost:3001

1. **Open Dashboard**:
   - Navigate to Dashboards
   - Select "Multi-Tier Cache Performance"

2. **Verify Panels**:
   - Cache Hit Rate panel should show %
   - Cache Operations panel should show hits/misses
   - Latency panel should show P95 values

3. **Verify Data**:
   - Panels should show data (not "No data")
   - Time range selector working
   - Auto-refresh working

---

## Troubleshooting

### Issue: No Data in Dashboard

**Check**:
1. Prometheus scraping configured?
   - http://localhost:9090/targets
2. Metrics endpoint accessible?
   - `curl http://localhost:8080/metrics`
3. Datasource connected in Grafana?
   - Configuration → Data Sources → Prometheus

**Fix**:
- Verify Prometheus datasource URL: `http://prometheus:9090` (Docker network)
- Test connection in Grafana

---

### Issue: Dashboard Import Fails

**Error**: "Dashboard model not valid"

**Fix**:
- Verify JSON is valid: `cat multi-tier-cache-performance.json | jq .`
- Remove `"id": null` from JSON (Grafana auto-generates)
- Verify datasource UID matches

---

### Issue: Metrics Not Updating

**Check**:
1. API is running and serving requests?
2. OpenTelemetry configured correctly?
3. Metrics enabled in configuration?

**Fix**:
```bash
# Verify OTEL configuration
grep -A 5 "AddOpenTelemetry" apps/api/src/Api/Extensions/ObservabilityServiceExtensions.cs

# Verify metrics endpoint
curl http://localhost:8080/metrics | grep meepleai_cache

# Restart API if needed
cd apps/api/src/Api && dotnet run
```

---

## Expected Results (Issue #3909 Validation)

### After Import

**Dashboard Panels Visible**:
- ✅ Cache Hit Rate gauge (target: > 80%)
- ✅ Cache Operations timeline (hits vs misses)
- ✅ Cache Latency P95 graph
- ✅ Dashboard Invalidations counter
- ✅ Cache Promotions (L2 → L1)
- ✅ TTL Adjustments by classification

**Metrics Flowing**:
- ✅ Data points visible (not "No data")
- ✅ Auto-refresh working (5s interval)
- ✅ Time range selector functional

**Performance**:
- ✅ Dashboard loads quickly (< 2s)
- ✅ Queries responsive
- ✅ No errors in Grafana logs

---

## Checkbox Completion (Issue #3909)

After successful import, mark as complete:

- [x] Prometheus metrics export implementation ✅
- [x] Grafana dashboard integration (dashboard JSON imported) ✅
- [x] Production cache hit rate measurement (> 80% target) ✅

**Validation Command**:
```bash
# Verify cache hit rate > 80%
# Query Prometheus:
curl 'http://localhost:9090/api/v1/query?query=sum(rate(meepleai_cache_hits_total[5m])) / (sum(rate(meepleai_cache_hits_total[5m])) + sum(rate(meepleai_cache_misses_total[5m]))) * 100'

# Expected result: > 80
```

---

**Created**: 2026-02-09
**Issues**: #3909, #3955 (Task #3 completion)
