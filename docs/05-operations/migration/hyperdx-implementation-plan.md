# HyperDX Implementation Plan

**Status**: Pre-Production Implementation
**Created**: 2025-11-21
**Owner**: DevOps Team
**Priority**: High

---

## Executive Summary

This document outlines the plan to implement **HyperDX** as the primary observability platform for MeepleAI **before production release**. HyperDX will handle logs, traces, and session replay, while Prometheus/Grafana/Alertmanager will handle infrastructure metrics and critical alerts.

**Key Decision**: **Direct Implementation** - Deploy HyperDX as the primary observability solution from day one, replacing Seq and Jaeger entirely.

**Rationale**: Since the system is not yet in production, we can choose the optimal observability stack without migration complexity or comparison overhead.

---

## Architecture Decision

### Final Observability Stack

```
Production Observability:
├── HyperDX                      # Logs, Traces, Session Replay, App Alerts
│   ├── Logs (replaces Seq)
│   ├── Traces (replaces Jaeger)
│   ├── Session Replay (new capability)
│   └── Application Alerts (errors, performance)
│
└── Grafana Stack                # Infrastructure Metrics & Critical Alerts
    ├── Prometheus               # Infrastructure metrics (CPU, RAM, disk)
    ├── Grafana                  # Business dashboards (RAG quality, analytics)
    └── Alertmanager             # Critical infrastructure alerts (DB down, OOM)
```

**Services Removed**:
- ❌ **Seq** → Replaced by HyperDX (logs)
- ❌ **Jaeger** → Replaced by HyperDX (traces)

**Services Retained**:
- ✅ **Prometheus** → Infrastructure metrics
- ✅ **Grafana** → Business dashboards
- ✅ **Alertmanager** → Critical infrastructure alerts

---

## Why HyperDX?

### Core Benefits

**1. Unified Platform**
- Single tool for logs + traces + session replay (vs 2 separate tools)
- Automatic correlation: click log → auto-open trace → view session replay
- Reduced operational overhead: 1 service instead of 2 (Seq + Jaeger)

**2. Session Replay**
- Record frontend user interactions automatically
- Correlate user sessions with backend errors and traces
- Debug production issues with full context (what user did → what broke)

**3. Full-Text Search**
- ClickHouse-powered search: <1s query time for millions of logs
- No LogQL required: simple full-text search interface
- High-cardinality support: efficiently handles millions of unique log streams

**4. Pattern Recognition**
- Automatic log clustering and anomaly detection
- Identify recurring error patterns without manual analysis
- Reduce noise by grouping similar events

**5. Cost Efficiency**
- Self-hosted: ~$25/month (2 CPU, 4GB RAM, 50GB storage)
- Cloud-hosted: $0.40/GB (10x cheaper than Datadog, 3x cheaper than New Relic)
- Zero per-seat or per-host fees

**6. OpenTelemetry Native**
- No vendor lock-in: uses standard OTel protocol (OTLP gRPC/HTTP)
- Easy migration if needed: just change exporter endpoint
- Compatible with existing .NET OTel SDK

---

## Implementation Plan

### Phase 1: Infrastructure Setup (Week 1)

#### Step 1.1: Deploy HyperDX Container

**File**: `infra/docker-compose.hyperdx.yml`

```yaml
services:
  meepleai-hyperdx:
    image: docker.hyperdx.io/hyperdx/hyperdx-local:2-beta
    container_name: meepleai-hyperdx
    restart: unless-stopped
    ports:
      - "8180:8080"   # Web UI (avoid conflict with meepleai-api:8080)
      - "14317:4317"  # OTLP gRPC (avoid conflict with Jaeger)
      - "14318:4318"  # OTLP HTTP (avoid conflict with Jaeger)
      - # "8123:8123" removed for security - use internal networking only (for backups)
    volumes:
      - hyperdx-data:/var/lib/clickhouse
      - hyperdx-logs:/var/log/hyperdx
    environment:
      # Core Settings
      - HYPERDX_API_KEY=${HYPERDX_API_KEY:?Error: HYPERDX_API_KEY required in production}
      - HYPERDX_LOG_LEVEL=info
      - HYPERDX_SERVICE_NAME=meepleai

      # Data Retention
      - HYPERDX_RETENTION_DAYS=30
      - HYPERDX_MAX_STORAGE_GB=50

      # Performance Tuning
      - CLICKHOUSE_MAX_MEMORY_USAGE=4GB
      - CLICKHOUSE_MAX_THREADS=4

      # Alerting
      - HYPERDX_ALERT_EMAIL=badsworm@gmail.com
      - HYPERDX_ALERT_SLACK_WEBHOOK=${SLACK_WEBHOOK_URL}

    healthcheck:
      test: ["CMD-SHELL", "wget --no-verbose --tries=1 --spider, "-f", "http://localhost:8180/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 60s

    networks:
      - meepleai

    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 4G
        reservations:
          cpus: '1.0'
          memory: 2G

volumes:
  hyperdx-data:
    driver: local
  hyperdx-logs:
    driver: local

networks:
  meepleai:
    name: meepleai
    driver: bridge
```

**Deployment**:
```bash
cd infra
docker compose -f docker-compose.yml -f docker-compose.hyperdx.yml up -d meepleai-hyperdx
```

**Verification**:
- [ ] UI accessible: http://localhost:8180
- [ ] OTel collector ready: `curl http://localhost:4318/v1/traces` (404 OK)
- [ ] Health check passing: `docker compose ps meepleai-hyperdx` (healthy)

---

#### Step 1.2: Remove Seq and Jaeger

**Update `infra/docker-compose.yml`**:

Remove or comment out Seq and Jaeger services:

```yaml
# REMOVED: Seq (logs replaced by HyperDX)
# meepleai-seq:
#   image: datalust/seq:2025.1
#   ...

# REMOVED: Jaeger (traces replaced by HyperDX)
# meepleai-jaeger:
#   image: jaegertracing/all-in-one:1.74.0
#   ...
```

**Verify Docker Compose**:
```bash
docker compose config  # Should parse without errors
```

---

### Phase 2: Backend Integration (Week 1)

#### Step 2.1: Configure .NET API OpenTelemetry

**File**: `apps/api/src/Api/Program.cs`

**Add NuGet Packages**:
```bash
cd apps/api/src/Api
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol --version 1.7.0
dotnet add package Serilog.Sinks.OpenTelemetry --version 1.0.0
```

**OpenTelemetry Configuration**:

```csharp
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

// Configure OpenTelemetry Resource
var resource = ResourceBuilder.CreateDefault()
    .AddService("meepleai-api", "1.0.0")
    .AddAttributes(new Dictionary<string, object>
    {
        ["deployment.environment"] = builder.Environment.EnvironmentName,
        ["service.namespace"] = "meepleai",
        ["service.instance.id"] = Environment.MachineName
    });

// Configure OpenTelemetry Tracing
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddResource(resource))
    .WithTracing(tracing =>
    {
        tracing
            // Instrumentation
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.Filter = (httpContext) =>
                {
                    // Don't trace health checks
                    return !httpContext.Request.Path.StartsWithSegments("/health");
                };
            })
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.SetDbStatementForText = true;
            })

            // HyperDX Exporter (OTLP gRPC)
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://meepleai-hyperdx:14317");
                options.Protocol = OtlpExportProtocol.Grpc;
                options.ExportProcessorType = ExportProcessorType.Batch;
                options.BatchExportProcessorOptions = new BatchExportProcessorOptions<Activity>
                {
                    MaxQueueSize = 2048,
                    ScheduledDelayMilliseconds = 5000,
                    ExporterTimeoutMilliseconds = 30000,
                    MaxExportBatchSize = 512
                };
            });
    })
    .WithMetrics(metrics =>
    {
        metrics
            // Instrumentation
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation()

            // Custom Metrics
            .AddMeter("MeepleAi.Api")

            // Exporters
            .AddPrometheusExporter()  // Keep for Grafana infrastructure metrics
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://meepleai-hyperdx:14317");
                options.Protocol = OtlpExportProtocol.Grpc;
            });
    });

// Configure Serilog Logging
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "meepleai-api")
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)

        // Console (for Docker logs)
        .WriteTo.Console()

        // HyperDX via OTLP HTTP
        .WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = "http://meepleai-hyperdx:14318/v1/logs";
            options.Protocol = OtlpProtocol.HttpProtobuf;
            options.ResourceAttributes = new Dictionary<string, object>
            {
                ["service.name"] = "meepleai-api",
                ["deployment.environment"] = context.HostingEnvironment.EnvironmentName
            };
        });
});

var app = builder.Build();

// Add trace context to responses for frontend correlation
app.Use(async (context, next) =>
{
    var activity = Activity.Current;
    if (activity != null)
    {
        context.Response.Headers.Append("X-Trace-Id", activity.TraceId.ToString());
        context.Response.Headers.Append("X-Span-Id", activity.SpanId.ToString());
    }
    await next();
});

app.Run();
```

**Verification**:
```bash
dotnet build
dotnet run
```

Check HyperDX UI (http://localhost:8180) for:
- [ ] Logs appearing from meepleai-api
- [ ] Traces appearing with spans
- [ ] Metrics being ingested

---

### Phase 3: Frontend Integration (Week 1)

#### Step 3.1: Install HyperDX Browser SDK

**File**: `apps/web/package.json`

```bash
cd apps/web
pnpm add @hyperdx/browser
```

---

#### Step 3.2: Create HyperDX Initialization Module

**File**: `apps/web/src/lib/hyperdx.ts`

```typescript
// HyperDX Browser SDK Configuration
import { HyperDX } from '@hyperdx/browser';

const isProduction = process.env.NODE_ENV === 'production';
const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

export function initializeHyperDX() {
  if (typeof window === 'undefined') {
    return; // Server-side, do nothing
  }

  HyperDX.init({
    apiKey: process.env.NEXT_PUBLIC_HYPERDX_API_KEY || 'demo',
    service: 'meepleai-web',

    // Trace Propagation (correlate frontend → backend)
    tracePropagationTargets: [
      /localhost:8080/,
      /api\.meepleai\.dev/,
      new RegExp(apiBase.replace(/https?:\/\//, '')),
    ],

    // Session Replay
    consoleCapture: true,
    advancedNetworkCapture: true,
    recordingSettings: {
      maskAllInputs: true,          // Mask sensitive inputs
      maskAllText: false,            // Don't mask regular text
      blockClass: 'hyperdx-block',   // Block elements with this class
      ignoreClass: 'hyperdx-ignore', // Ignore elements with this class
    },

    // Performance
    sampleRate: isProduction ? 0.1 : 1.0, // 10% sampling in production

    // Privacy
    beforeSend: (event) => {
      // Scrub sensitive data before sending
      if (event.request?.body) {
        event.request.body = scrubSensitiveData(event.request.body);
      }
      return event;
    },
  });
}

function scrubSensitiveData(data: any): any {
  const sensitiveKeys = ['password', 'token', 'apiKey', 'secret'];

  if (typeof data === 'object' && data !== null) {
    const scrubbed = { ...data };
    for (const key of Object.keys(scrubbed)) {
      if (sensitiveKeys.some(sk => key.toLowerCase().includes(sk))) {
        scrubbed[key] = '[REDACTED]';
      } else if (typeof scrubbed[key] === 'object') {
        scrubbed[key] = scrubSensitiveData(scrubbed[key]);
      }
    }
    return scrubbed;
  }

  return data;
}

// Custom Event Tracking
export function trackEvent(eventName: string, properties?: Record<string, any>) {
  HyperDX.addAction(eventName, properties);
}

// User Identification (call after login)
export function identifyUser(userId: string, userEmail?: string) {
  HyperDX.setGlobalAttributes({
    userId,
    userEmail: userEmail || undefined,
  });
}
```

---

#### Step 3.3: Initialize in Root Layout

**File**: `apps/web/src/app/layout.tsx`

```tsx
'use client';

import { initializeHyperDX } from '@/lib/hyperdx';
import { useEffect } from 'react';

export default function RootLayout({ children }: { children: React.ReactNode }) {
  useEffect(() => {
    initializeHyperDX();
  }, []);

  return (
    <html lang="it">
      <body>
        <AppProviders>
          {children}
        </AppProviders>
      </body>
    </html>
  );
}
```

---

#### Step 3.4: Add User Identification After Login

**File**: `apps/web/src/components/auth/AuthProvider.tsx`

```tsx
import { identifyUser } from '@/lib/hyperdx';

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();

  useEffect(() => {
    if (user) {
      // Identify user in HyperDX for session tracking
      identifyUser(user.id, user.email);
    }
  }, [user]);

  return <>{children}</>;
}
```

---

#### Step 3.5: Update Environment Variables

**File**: `apps/web/.env.example`

Add HyperDX configuration:

```bash
# Existing variables
NEXT_PUBLIC_API_BASE=http://localhost:8080
NEXT_PUBLIC_DEMO_EMAIL=user@meepleai.dev

# HyperDX Observability (NEW)
NEXT_PUBLIC_HYPERDX_API_KEY=demo
```

**File**: `apps/web/.env.local` (create if not exists)

```bash
NEXT_PUBLIC_HYPERDX_API_KEY=demo
```

---

### Phase 4: Alert Configuration (Week 2)

#### Step 4.1: Application Alerts (HyperDX)

**Create Alerts in HyperDX UI** (http://localhost:8180/alerts):

**Alert 1: High Error Rate (Critical)**

```json
{
  "name": "High Error Rate - API",
  "condition": {
    "query": "service.name:meepleai-api AND level:error",
    "aggregation": "count",
    "threshold": 100,
    "window": "5m"
  },
  "channels": [
    {
      "type": "email",
      "to": ["badsworm@gmail.com"]
    },
    {
      "type": "slack",
      "webhook": "${SLACK_WEBHOOK_URL}",
      "channel": "#alerts"
    }
  ],
  "severity": "critical",
  "message": "🚨 High error rate detected: {{count}} errors in {{window}}"
}
```

**Alert 2: Slow API Response (Warning)**

```json
{
  "name": "Slow API Response",
  "condition": {
    "query": "service.name:meepleai-api AND http.server.duration > 3000",
    "aggregation": "p95",
    "threshold": 3000,
    "window": "5m"
  },
  "channels": [
    {
      "type": "slack",
      "webhook": "${SLACK_WEBHOOK_URL}",
      "channel": "#performance"
    }
  ],
  "severity": "warning",
  "message": "⚠️ API response time degraded: P95={{p95}}ms"
}
```

**Alert 3: Frontend Error Spike (Critical)**

```json
{
  "name": "Frontend Error Spike",
  "condition": {
    "query": "service.name:meepleai-web AND error:true",
    "aggregation": "count",
    "threshold": 50,
    "window": "5m"
  },
  "channels": [
    {
      "type": "email",
      "to": ["badsworm@gmail.com"]
    }
  ],
  "severity": "critical",
  "message": "🚨 Frontend error spike: {{count}} errors in {{window}}"
}
```

---

#### Step 4.2: Infrastructure Alerts (Alertmanager)

**Keep Critical Infrastructure Alerts in Alertmanager**:

**File**: `infra/prometheus/alerts/infrastructure.yml`

```yaml
groups:
  - name: infrastructure
    interval: 30s
    rules:
      # Database Health
      - alert: DatabaseDown
        expr: up{job="postgres"} == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "PostgreSQL database is down"
          description: "Database {{ $labels.instance }} has been down for >1 minute"

      # Redis Health
      - alert: RedisDown
        expr: up{job="redis"} == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Redis cache is down"

      # Qdrant Health
      - alert: QdrantDown
        expr: up{job="qdrant"} == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Qdrant vector database is down"

      # High Memory Usage
      - alert: HighMemoryUsage
        expr: (1 - (node_memory_MemAvailable_bytes / node_memory_MemTotal_bytes)) * 100 > 90
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High memory usage detected"

      # Disk Space Low
      - alert: DiskSpaceLow
        expr: (node_filesystem_avail_bytes / node_filesystem_size_bytes) * 100 < 10
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Disk space critically low"
```

---

### Phase 5: Testing & Validation (Week 2)

#### Step 5.1: Integration Tests

**Test Logs Ingestion**:
```bash
# Generate test logs from API
curl -X POST http://localhost:8080/api/v1/test-error

# Verify in HyperDX UI (http://localhost:8180)
# Search: service.name:meepleai-api AND level:error
```

**Test Trace Correlation**:
```bash
# Make API request with tracing
curl -X GET http://localhost:8080/api/v1/games

# In HyperDX:
# 1. Find log entry for GET /games
# 2. Click "View Trace" button
# 3. Should auto-open distributed trace view
```

**Test Session Replay**:
```bash
# Open web app
http://localhost:3000

# Perform actions:
# 1. Navigate to /games
# 2. Click on a game
# 3. Trigger an error (e.g., invalid form input)

# In HyperDX:
# 1. Go to Sessions tab
# 2. Find your session (by user ID or time)
# 3. Click "Replay" to watch recording
# 4. Should see your actions + correlated backend errors
```

---

#### Step 5.2: Performance Tests

**Load Test with k6**:

**File**: `tests/k6/hyperdx-ingestion-test.js`

```javascript
import http from 'k6/http';
import { check } from 'k6';

export const options = {
  stages: [
    { duration: '1m', target: 100 },  // Ramp up to 100 users
    { duration: '3m', target: 100 },  // Stay at 100 users
    { duration: '1m', target: 0 },    // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'], // 95% of requests < 500ms
  },
};

export default function () {
  // Generate API traffic to create logs/traces
  const res = http.get('http://localhost:8080/api/v1/games');

  check(res, {
    'status is 200': (r) => r.status === 200,
    'response time < 500ms': (r) => r.timings.duration < 500,
  });
}
```

**Run Load Test**:
```bash
cd tests/k6
k6 run hyperdx-ingestion-test.js
```

**Verify in HyperDX**:
- [ ] All requests logged (no data loss)
- [ ] Search performance remains <1s
- [ ] HyperDX resource usage <4GB RAM
- [ ] No OTel exporter errors in API logs

---

#### Step 5.3: Alert Testing

**Trigger High Error Rate Alert**:
```bash
# Generate 100+ errors in 5 minutes
for i in {1..150}; do
  curl -X POST http://localhost:8080/api/v1/test-error &
  sleep 2
done
```

**Expected Result**:
- [ ] Email sent to badsworm@gmail.com within 2 minutes
- [ ] Slack notification in #alerts channel
- [ ] Alert visible in HyperDX UI

---

### Phase 6: Documentation Updates (Week 2)

#### Step 6.1: Update Project Documentation

**Files to Update**:

1. **`CLAUDE.md`** - Update observability section:

```markdown
## Observability

**Primary Tools**:
- **HyperDX** (http://localhost:8180): Logs, Traces, Session Replay, App Alerts
- **Prometheus** (http://localhost:9090): Infrastructure Metrics
- **Grafana** (http://localhost:3001): Business Dashboards
- **Alertmanager** (http://localhost:9093): Critical Infrastructure Alerts

**Deprecated**:
- ~~Seq~~ → Use HyperDX for logs
- ~~Jaeger~~ → Use HyperDX for traces
```

2. **`docs/05-operations/README.md`**:

```markdown
## Observability Stack

**Application Observability** (HyperDX):
- Logs: Full-text search, pattern clustering
- Traces: Distributed tracing, span analysis
- Session Replay: Frontend user interaction recording
- Alerts: Application errors, performance degradation

**Infrastructure Observability** (Grafana Stack):
- Metrics: Prometheus (CPU, RAM, disk, network)
- Dashboards: Grafana (RAG quality, user analytics)
- Alerts: Alertmanager (database down, OOM, disk full)
```

3. **`docs/05-operations/runbooks/*.md`**:

Update all runbook links:
- Replace `http://localhost:8081` (Seq) → `http://localhost:8180` (HyperDX)
- Replace `http://localhost:16686` (Jaeger) → `http://localhost:8180` (HyperDX)

4. **`infra/docker-compose.yml`**:

Add comment explaining removal:
```yaml
# Observability Stack
# - HyperDX: Logs, Traces, Session Replay (see docker-compose.hyperdx.yml)
# - Prometheus: Infrastructure metrics
# - Grafana: Business dashboards
# - Alertmanager: Critical infrastructure alerts
#
# REMOVED (2025-11-21):
# - Seq (logs): Replaced by HyperDX
# - Jaeger (traces): Replaced by HyperDX
```

---

#### Step 6.2: Create ADR

**File**: `docs/01-architecture/adr/adr-015-hyperdx-observability.md`

```markdown
# ADR-015: HyperDX for Application Observability

**Status**: Accepted
**Date**: 2025-11-21
**Deciders**: DevOps Team

## Context

MeepleAI requires comprehensive observability for logs, traces, and session replay
before production launch. We evaluated multiple solutions:

**Options Considered**:
1. **Grafana-Native Stack**: Seq + Jaeger + Loki + Tempo + Grafana
2. **HyperDX**: Unified platform (logs + traces + session replay)
3. **Datadog**: Commercial SaaS platform

## Decision

**Chosen**: **HyperDX** for application observability (logs, traces, session replay)

**Retained**: Prometheus/Grafana/Alertmanager for infrastructure metrics

## Rationale

**Why HyperDX**:
1. **Unified Platform**: 1 tool vs 2 (Seq + Jaeger) - reduced operational overhead
2. **Session Replay**: Critical for debugging frontend issues (not available in Grafana stack)
3. **Full-Text Search**: ClickHouse-powered, <1s queries (vs LogQL complexity)
4. **Auto-Correlation**: Click log → auto-open trace (vs manual trace ID copy)
5. **Cost**: Self-hosted ~$25/month (vs $0 Grafana, but missing features)
6. **OpenTelemetry**: No vendor lock-in, standard protocol

**Why Not Grafana Stack**:
- Missing session replay (critical for frontend debugging)
- Manual correlation required (copy trace ID from logs → Jaeger UI)
- Higher operational complexity (2+ services: Seq + Jaeger)
- No automatic pattern recognition or log clustering

**Why Not Datadog**:
- Cost: $15/host + $0.10/GB = ~$250/month (10x more expensive)
- Vendor lock-in: proprietary agent and protocol
- Overkill for pre-production MVP

## Consequences

**Positive**:
- ✅ Unified observability experience for developers
- ✅ Session replay enables faster frontend debugging
- ✅ Automatic log-trace-session correlation
- ✅ Reduced service count (4 vs 5 observability services)

**Negative**:
- ❌ Additional $25/month cost vs free Grafana stack (acceptable for MVP)
- ❌ Smaller community than Grafana (but Y Combinator + ClickHouse backing)
- ❌ New tool for team to learn (mitigated by better UX)

**Neutral**:
- Hybrid architecture: HyperDX (app) + Grafana (infra) adds slight complexity
- OpenTelemetry ensures easy migration if HyperDX doesn't scale

## Implementation

See `docs/05-operations/migration/hyperdx-implementation-plan.md` for details.

**Timeline**: 2 weeks (Week 1: deployment, Week 2: testing/docs)
```

---

## Success Metrics

### Technical Metrics (Absolute Targets)

**Performance**:
- ✅ Log search time: <1s P95 (ClickHouse query performance)
- ✅ Trace query time: <500ms P95 (span analysis)
- ✅ Session replay load time: <3s (video playback)
- ✅ Alert latency: <1 min (error → notification)

**Reliability**:
- ✅ Telemetry ingestion: 99.9% success rate (no data loss)
- ✅ HyperDX uptime: 99.9% (during MVP period)
- ✅ Resource usage: <4GB RAM, <2 CPU cores
- ✅ Data retention: 30 days (logs/traces), 7 days (session replay)

**Coverage**:
- ✅ Log ingestion: 100% of API logs captured
- ✅ Trace coverage: 100% of HTTP requests traced
- ✅ Session replay: 100% of user sessions recorded (dev), 10% (prod)
- ✅ Alert coverage: All critical errors trigger notifications

### Operational Metrics

**Developer Productivity**:
- ✅ Onboarding time: New engineers productive with HyperDX in <1 day
- ✅ Debugging efficiency: Engineers can find root cause in <10 min
- ✅ Tool satisfaction: ≥80% of team prefer HyperDX over Seq/Jaeger

**Business Impact**:
- ✅ Issue detection: 100% of customer-impacting issues detected
- ✅ False positive rate: <10% of alerts are false positives
- ✅ MTTR (Mean Time To Resolution): <15 minutes for critical issues
- ✅ MTTD (Mean Time To Detect): <5 minutes for critical issues

---

## Rollback Plan

### Pre-Production Rollback

**If HyperDX fails during implementation**:

1. **Stop HyperDX**:
   ```bash
   docker compose -f docker-compose.hyperdx.yml down
   ```

2. **Restore Seq + Jaeger** in `docker-compose.yml`:
   ```bash
   git checkout main -- infra/docker-compose.yml
   docker compose up -d meepleai-seq meepleai-jaeger
   ```

3. **Update API configuration**:
   ```bash
   git checkout main -- apps/api/src/Api/Program.cs
   dotnet build && dotnet run
   ```

4. **Remove HyperDX browser SDK**:
   ```bash
   cd apps/web
   pnpm remove @hyperdx/browser
   git checkout main -- src/app/layout.tsx
   ```

**Estimated Rollback Time**: 15 minutes
**Impact**: Zero (system not in production)

---

## Risk Assessment

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| **Data Loss During Setup** | Low | High | Test with synthetic data first, verify 100% ingestion |
| **Performance Issues** | Low | Medium | Load test before production, set resource limits |
| **Team Learning Curve** | Medium | Low | Provide training, comprehensive documentation |
| **ClickHouse Storage Cost** | Low | Medium | Set 30-day retention, monitor disk usage |
| **Vendor Lock-In** | Low | High | Use OpenTelemetry (easy to switch exporters) |
| **HyperDX Unavailable** | Low | High | Multi-region deployment (future), Prometheus as fallback |

---

## Timeline

### 2-Week Implementation Schedule

**Week 1: Infrastructure & Backend**

| Day | Task | Owner | Status |
|-----|------|-------|--------|
| 1 | Deploy HyperDX container | DevOps | Pending |
| 1 | Remove Seq + Jaeger from docker-compose.yml | DevOps | Pending |
| 2 | Add OTel exporters to .NET API | Backend | Pending |
| 2 | Configure Serilog → HyperDX | Backend | Pending |
| 3 | Verify logs ingestion | Backend | Pending |
| 3 | Verify traces ingestion | Backend | Pending |
| 4 | Add trace context to HTTP responses | Backend | Pending |
| 5 | Integration testing (logs + traces) | QA | Pending |

**Week 2: Frontend & Documentation**

| Day | Task | Owner | Status |
|-----|------|-------|--------|
| 6 | Install HyperDX browser SDK | Frontend | Pending |
| 6 | Initialize in root layout | Frontend | Pending |
| 7 | Add user identification after login | Frontend | Pending |
| 7 | Test session replay | Frontend | Pending |
| 8 | Configure HyperDX alerts | DevOps | Pending |
| 9 | Load testing (k6) | QA | Pending |
| 10 | Update documentation (CLAUDE.md, README) | All | Pending |
| 11 | Create ADR-015 | DevOps | Pending |
| 12 | Final validation, project closeout | All | Pending |

---

## Cost Analysis

### Self-Hosted HyperDX

**Infrastructure** (Docker host):
- **Compute**: 2 CPU cores, 4GB RAM → ~$20/month (DigitalOcean Droplet)
- **Storage**: 50GB SSD (ClickHouse data) → ~$5/month
- **Network**: 1TB transfer → Included

**Total**: ~$25/month

### Comparison (for reference)

**Current Stack** (Seq + Jaeger + Prometheus + Grafana + Alertmanager):
- Cost: $0 (all open-source)
- Services: 5 containers
- Operational Overhead: High (5 services to maintain)

**HyperDX** (Logs + Traces + Session Replay):
- Cost: ~$25/month (self-hosted)
- Services: 1 container (+ Prometheus/Grafana/Alertmanager for infra)
- Operational Overhead: Low (1 service for app observability)

**Datadog** (for reference):
- Cost: $15/host + $0.10/GB × 100GB = ~$250/month (10x more)
- Services: 0 (SaaS)
- Operational Overhead: Minimal

**ROI Analysis**:
- $25/month investment in HyperDX yields:
  - Session replay (critical for debugging)
  - Faster issue resolution (20%+ MTTR reduction)
  - Reduced operational overhead (4 services vs 5)
  - Better developer experience (unified platform)

---

## Retention & Storage Management

### ClickHouse Retention Policies

**Automated TTL** (HyperDX applies automatically):

```sql
-- Logs: 30 days retention
ALTER TABLE logs MODIFY TTL timestamp + INTERVAL 30 DAY;

-- Traces: 30 days retention
ALTER TABLE traces MODIFY TTL timestamp + INTERVAL 30 DAY;

-- Metrics: 90 days retention (aggregated)
ALTER TABLE metrics MODIFY TTL timestamp + INTERVAL 90 DAY;

-- Session Replay: 7 days retention (large storage)
ALTER TABLE sessions MODIFY TTL timestamp + INTERVAL 7 DAY;
```

### Storage Monitoring

**Check Disk Usage**:

```bash
# ClickHouse storage breakdown
docker exec meepleai-hyperdx clickhouse-client --query "
  SELECT
    database,
    table,
    formatReadableSize(sum(bytes)) AS size
  FROM system.parts
  WHERE active
  GROUP BY database, table
  ORDER BY sum(bytes) DESC
"
```

**Expected Storage** (30-day retention):
- Logs: ~15-20 GB
- Traces: ~8-12 GB
- Metrics: ~2-3 GB
- Session Replay: ~20-30 GB (if 100% sampling)

**Total**: ~50GB (within 50GB budget)

---

## Appendix

### A. HyperDX vs Grafana Stack Comparison

| Feature | HyperDX | Grafana Stack | Winner |
|---------|---------|---------------|--------|
| **Logs** | ClickHouse, full-text search | Loki, LogQL queries | HyperDX |
| **Traces** | Distributed tracing, spans | Tempo, TraceQL | Tie |
| **Session Replay** | ✅ Built-in | ❌ Not available | HyperDX |
| **Correlation** | Automatic (click log → trace) | Manual (copy trace ID) | HyperDX |
| **Search Speed** | <1s (ClickHouse) | 3-5s (depends on index) | HyperDX |
| **Pattern Recognition** | ✅ Auto-clustering | ❌ Manual | HyperDX |
| **Service Count** | 1 service | 2+ services (Loki + Tempo) | HyperDX |
| **Cost** | ~$25/month | $0 (open-source) | Grafana |
| **Community** | Small (Y Combinator) | Large (CNCF) | Grafana |
| **Vendor Lock-In** | Low (OpenTelemetry) | None (OSS) | Grafana |

**Conclusion**: HyperDX wins on features and UX, Grafana wins on cost and community.

---

### B. OpenTelemetry Configuration Reference

See **Phase 2, Step 2.1** for complete .NET API configuration.

---

### C. Next.js HyperDX Configuration Reference

See **Phase 3, Step 3.2** for complete browser SDK setup.

---

### D. Alert Configuration Examples

See **Phase 4** for HyperDX and Alertmanager alert configurations.

---

## References

### Official Documentation

- **HyperDX Docs**: https://www.hyperdx.io/docs
- **HyperDX GitHub**: https://github.com/hyperdxio/hyperdx
- **OpenTelemetry .NET**: https://opentelemetry.io/docs/languages/net/
- **ClickHouse Docs**: https://clickhouse.com/docs

### Internal Documentation

- **CLAUDE.md**: Project overview and stack
- **OPS-05 Setup**: `docs/05-operations/ops-05-setup.md` (Grafana-Native Stack reference)
- **Observability README**: `docs/05-operations/README.md`
- **Infrastructure Diagram**: `docs/01-architecture/diagrams/infrastructure-overview.md`

### Related Issues

- **TBD**: Create tracking issue for HyperDX implementation

---

**Document Status**: Ready for Implementation
**Last Updated**: 2025-12-13T10:59:23.970Z
**Next Review**: After Week 1 completion (infrastructure + backend)

