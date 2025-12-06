# HyperDX Data Source Setup Guide

**Purpose**: Configure ClickHouse connection and data sources in HyperDX v2-beta
**Related Issue**: #1567 (prerequisite for alert configuration)
**Time Required**: 5-10 minutes

---

## Step 1: Create Server Connection to ClickHouse

### Connection Parameters

When HyperDX asks for a "Server Connection" or "Database Connection", use these exact values:

| Parameter | Value | Notes |
|-----------|-------|-------|
| **Connection Name** | `Default` | Standard name for primary connection |
| **Type/Database** | `ClickHouse` | Select ClickHouse from dropdown |
| **Host** | `ch-server` | Internal ClickHouse hostname in hyperdx-local container |
| **Port** | `9000` | ClickHouse native protocol port (NOT 8123 HTTP) |
| **Protocol** | `Native` or `TCP` | Use native protocol, not HTTP |
| **Database** | `default` | The database containing OpenTelemetry tables |
| **Username** | `default` | Default ClickHouse username |
| **Password** | *(leave empty)* | No password required for hyperdx-local setup |
| **Secure/TLS** | `Off` or `No` | Local connection, no encryption needed |

### Test Connection

After entering these values:
1. Click **"Test Connection"** button
2. Should see: ✅ **"Connection successful"** or similar
3. If error occurs, verify:
   - HyperDX container is running: `docker ps | grep hyperdx`
   - ClickHouse is accessible: `docker exec meepleai-hyperdx sh -c "clickhouse-client --host ch-server --query 'SELECT 1'"`

---

## Step 2: Configure Data Sources

Once the Server Connection is created, HyperDX should auto-detect the OpenTelemetry tables.

### Logs Data Source

**If prompted to configure manually:**

| Field | Value | Auto-Detected? |
|-------|-------|----------------|
| **Name** | `HyperDX Logs` | No |
| **Server Connection** | `Default` | No |
| **Database** | `default` | Yes |
| **Table** | `otel_logs` | **Enter manually** |
| **Timestamp Column** | `Timestamp` | Yes |
| **Service Name Expression** | `ServiceName` | Yes |
| **Log Level Expression** | `SeverityText` | Yes |
| **Body Expression** | `Body` | Yes |
| **Trace ID Expression** | `TraceId` | Yes |
| **Span ID Expression** | `SpanId` | Yes |

**Click**: `Save New Source`

### Traces Data Source

**If prompted to configure manually:**

| Field | Value | Auto-Detected? |
|-------|-------|----------------|
| **Name** | `HyperDX Traces` | No |
| **Server Connection** | `Default` | No |
| **Database** | `default` | Yes |
| **Table** | `otel_traces` | **Enter manually** |
| **Timestamp Column** | `Timestamp` | Yes |
| **Trace ID Expression** | `TraceId` | Yes |
| **Span ID Expression** | `SpanId` | Yes |
| **Parent Span ID Expression** | `ParentSpanId` | Yes |
| **Span Name Expression** | `SpanName` | Yes |
| **Service Name Expression** | `ServiceName` | Yes |
| **Duration Expression** | `Duration` | Yes |

**Click**: `Save New Source`

### Metrics Data Sources

**If HyperDX asks to configure metrics sources**, configure these tables:

| Metric Type | Table Name |
|-------------|-----------|
| **Gauge** | `otel_metrics_gauge` |
| **Histogram** | `otel_metrics_histogram` |
| **Sum** | `otel_metrics_sum` |

Use the same `Default` server connection for all.

---

## Step 3: Verify Data Sources

After configuration:

1. Go to **Search** (or main navigation)
2. You should see logs appearing in the search interface
3. Go to **Traces** - you should see trace data
4. If no data visible yet, generate traffic:
   ```bash
   # Generate API traffic to create logs/traces
   for i in {1..30}; do
     curl http://localhost:8080/health
     sleep 1
   done
   ```

---

## Troubleshooting

### "No databases available" Error

**Cause**: Server connection not configured yet

**Fix**: Follow Step 1 to create the ClickHouse server connection

### "Tables not found" Error

**Cause**: ClickHouse tables don't exist yet (OpenTelemetry data not ingested)

**Fix**:
1. Verify tables exist:
   ```bash
   docker exec meepleai-hyperdx sh -c "clickhouse-client --host ch-server --query 'SHOW TABLES FROM default'"
   ```
2. If tables exist, manually enter table name `otel_logs` in the configuration
3. If tables don't exist, generate traffic to trigger table creation

### Connection Test Fails

**Cause**: ClickHouse not accessible or wrong credentials

**Debugging**:
```bash
# Test ClickHouse is running
docker exec meepleai-hyperdx sh -c "clickhouse-client --host ch-server --query 'SELECT version()'"

# Should return ClickHouse version (e.g., 24.10.2)
```

**Common Issues**:
- Host should be `ch-server` (not `localhost` or `clickhouse`)
- Port should be `9000` (not `8123`)
- Protocol should be `Native` (not `HTTP`)

---

## What Happens After Configuration

Once data sources are configured:

1. **Search View**: Logs appear in real-time search interface
2. **Traces View**: Distributed traces with span timelines
3. **Alerts**: Can now create alerts on logs/traces (proceed to alert configuration)
4. **Dashboards**: Can create custom dashboards with metrics

---

## Next Steps

After successfully configuring data sources:

1. ✅ Verify logs and traces are visible in HyperDX UI
2. ✅ Proceed to alert configuration: `hyperdx-alert-configuration.md`
3. ✅ Configure the 3 application alerts as specified in Issue #1567

---

## Related Documentation

- **Alert Configuration**: `docs/05-operations/runbooks/hyperdx-alert-configuration.md`
- **HyperDX Implementation Plan**: `docs/05-operations/migration/hyperdx-implementation-plan.md`
- **Issue #1567**: Configure HyperDX Application Alerts
- **Issue #1566**: Implement HyperDX Browser SDK (prerequisite)

---

## Changelog

- **2025-12-06**: Initial guide created for HyperDX v2-beta data source setup
