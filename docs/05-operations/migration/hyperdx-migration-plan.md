# HyperDX Migration Plan

**Status**: Planning Phase
**Created**: 2025-11-21
**Owner**: DevOps Team
**Priority**: Medium

---

## Executive Summary

This document outlines the plan to integrate **HyperDX** as a unified observability platform for MeepleAI, complementing or potentially replacing parts of the current Grafana-Native Stack (Prometheus + Grafana + Loki + Tempo + Alertmanager + Seq + Jaeger).

**Key Decision**: **Complement First, Replace Later** - Add HyperDX alongside existing stack for evaluation before making replacement decisions.

---

## Current State Analysis

### Existing Observability Stack

**Infrastructure** (from `infra/docker-compose.yml`):
```
Current Services:
├── meepleai-seq:8081           # Logs (Serilog → Seq)
├── meepleai-jaeger:16686       # Traces (OpenTelemetry → Jaeger)
├── meepleai-prometheus:9090    # Metrics (Prometheus)
├── meepleai-alertmanager:9093  # Alerts (Alertmanager)
└── meepleai-grafana:3001       # Dashboards (Grafana)
```

**Strengths**:
- ✅ Zero incremental cost (all open-source)
- ✅ Deep integration with existing OPS-01/OPS-02 infrastructure
- ✅ 12 pre-configured alert rules + 5 recording rules
- ✅ Comprehensive Grafana dashboards (11 panels for error monitoring)
- ✅ Email notifications via Alertmanager (Gmail SMTP)
- ✅ CI/CD validation (Prometheus rules, Alertmanager config, Grafana dashboards)

**Weaknesses**:
- ❌ **Fragmented Experience**: 5 separate tools (Seq, Jaeger, Prometheus, Alertmanager, Grafana)
- ❌ **Manual Correlation**: Engineers must manually connect logs → traces → metrics
- ❌ **No Session Replay**: Missing user session context for frontend issues
- ❌ **No Full-Text Search**: Loki-based approach requires metadata labels
- ❌ **High Operational Overhead**: 5 services to configure, maintain, upgrade
- ❌ **No Pattern Recognition**: No automatic log clustering or anomaly detection
- ❌ **Limited Frontend Observability**: Missing browser-side error tracking

---

## HyperDX Value Proposition

### Why HyperDX?

**Unified Platform Benefits**:
1. **Session Replay Correlation**: Automatically link user sessions → backend logs → traces
2. **Full-Text Search**: Lightning-fast search powered by ClickHouse (no LogQL required)
3. **Pattern Recognition**: Automatic log clustering and anomaly detection
4. **Reduced Complexity**: 1 tool instead of 5 (HyperDX replaces Seq + Jaeger + parts of Grafana)
5. **Cost Efficiency**: $0.40/GB (10x cheaper than Datadog, 3x cheaper than New Relic)
6. **OpenTelemetry Native**: No vendor lock-in, seamless integration with existing OTel SDK

### HyperDX Capabilities

**Core Features**:
- **Session Replay**: Frontend user interactions recorded and correlated with backend
- **Distributed Tracing**: End-to-end request tracking (browser → API → database)
- **Log Management**: Full-text search, pattern clustering, live tail
- **Metrics Visualization**: Chart builder for logs, metrics, traces
- **Alerting**: Slack, Email, PagerDuty integration
- **High-Cardinality Support**: Efficiently handles millions of unique log streams

**Technical Stack**:
- **Backend**: ClickHouse (columnar database for fast analytics)
- **Protocol**: OpenTelemetry (OTLP gRPC/HTTP on ports 4317/4318)
- **Deployment**: Docker Compose + Kubernetes support
- **Languages Supported**: Node.js, .NET, Python, Java, Golang, Browser (Next.js ✅)

---

## Migration Strategy

### Approach: **3-Phase Gradual Migration**

#### **Phase 1: Evaluation (Weeks 1-2)** - Add HyperDX Alongside Existing Stack

**Goal**: Validate HyperDX capabilities without disrupting current monitoring.

**Actions**:
1. Deploy HyperDX via Docker Compose (self-hosted)
2. Configure OpenTelemetry SDK to send telemetry to **both** HyperDX and existing stack (multi-exporter)
3. Add HyperDX browser SDK to Next.js app for session replay
4. Run in parallel for 2 weeks to evaluate:
   - Search performance (HyperDX ClickHouse vs Seq)
   - Trace correlation (HyperDX vs Jaeger)
   - Alert latency (HyperDX vs Alertmanager)
   - Resource usage (CPU, RAM, disk)

**Success Criteria**:
- ✅ HyperDX successfully ingests 100% of telemetry data
- ✅ Session replay correctly correlates with backend traces
- ✅ Search queries return results <1s for 90% of queries
- ✅ Resource usage stays within acceptable limits (CPU <50%, RAM <4GB)

**Rollback**: Simply stop sending telemetry to HyperDX, no impact on existing stack.

---

#### **Phase 2: Partial Replacement (Weeks 3-4)** - Replace Seq + Jaeger

**Goal**: Consolidate logs and traces into HyperDX while keeping metrics/alerts in Grafana stack.

**Actions**:
1. Stop Seq and Jaeger services (save ~2GB RAM)
2. Update documentation to point to HyperDX for logs/traces
3. Keep Prometheus + Grafana for metrics (business metrics, infrastructure metrics)
4. Keep Alertmanager for critical alerts (database down, high error rate)
5. Configure HyperDX alerts for application-level issues (error spikes, slow queries)

**Architecture After Phase 2**:
```
HyperDX:
├── Logs (replaces Seq)
├── Traces (replaces Jaeger)
├── Session Replay (new capability)
└── Application Alerts (new capability)

Grafana Stack (retained):
├── Prometheus (metrics)
├── Alertmanager (critical infrastructure alerts)
└── Grafana (dashboards for business metrics)
```

**Success Criteria**:
- ✅ Engineers use HyperDX as primary tool for debugging application issues
- ✅ Mean Time To Resolution (MTTR) reduced by 20% vs baseline
- ✅ No customer-impacting incidents missed by HyperDX alerts

**Rollback**: Restart Seq + Jaeger, reconfigure OTel exporters.

---

#### **Phase 3: Full Integration (Weeks 5-6)** - Unified Observability

**Goal**: Decide on final architecture based on Phase 1-2 learnings.

**Option A: Keep Hybrid** (Recommended for Phase 3)
- HyperDX: Logs, Traces, Session Replay, Application Alerts
- Prometheus: Infrastructure metrics (CPU, RAM, disk, network)
- Grafana: Business dashboards (RAG quality, user analytics)
- Alertmanager: Critical infrastructure alerts only

**Option B: Full HyperDX**
- Migrate all metrics to HyperDX (use OTel metrics exporter)
- Decommission Prometheus, Grafana, Alertmanager
- Single observability platform (simplest architecture)

**Option C: Revert to Grafana Stack**
- If HyperDX doesn't meet requirements, remove it
- Restore Seq + Jaeger, continue with existing stack

**Decision Criteria**:
- **Cost**: Total infrastructure cost (cloud storage + compute)
- **Team Productivity**: Developer feedback on debugging efficiency
- **Operational Overhead**: Time spent on observability maintenance
- **Feature Gaps**: Missing capabilities in either platform

---

## Implementation Plan

### Pre-Requisites

**Infrastructure**:
- [ ] Docker host with ≥4GB RAM, ≥2 CPU cores available
- [ ] Open ports: 8080 (UI), 4317 (OTLP gRPC), 4318 (OTLP HTTP)
- [ ] Persistent storage for ClickHouse data (≥50GB recommended)

**Dependencies**:
- [ ] OpenTelemetry SDK already installed (✅ confirmed in `apps/api`)
- [ ] Next.js app with React 19 (✅ confirmed in `apps/web`)
- [ ] Environment variables configured (`.env.example` updated)

### Phase 1 Implementation Steps

#### Step 1: Deploy HyperDX (Docker Compose)

**File**: `infra/docker-compose.hyperdx.yml` (new file)

```yaml
services:
  meepleai-hyperdx:
    image: docker.hyperdx.io/hyperdx/hyperdx-local:2-beta
    container_name: meepleai-hyperdx
    restart: unless-stopped
    ports:
      - "8080:8080"   # Web UI
      - "4317:4317"   # OTLP gRPC
      - "4318:4318"   # OTLP HTTP
      - "8123:8123"   # ClickHouse HTTP (optional)
    volumes:
      - hyperdx-data:/var/lib/clickhouse
    environment:
      - HYPERDX_API_KEY=${HYPERDX_API_KEY:-demo}
      - HYPERDX_LOG_LEVEL=info
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 60s
    networks:
      - meepleai

volumes:
  hyperdx-data:
    driver: local

networks:
  meepleai:
    external: true
```

**Start HyperDX**:
```bash
cd infra
docker compose -f docker-compose.yml -f docker-compose.hyperdx.yml up -d meepleai-hyperdx
```

**Verify**:
- UI: http://localhost:8080
- OTel Collector: http://localhost:4318/v1/traces (should return 404, collector is ready)

---

#### Step 2: Configure .NET API (Backend)

**File**: `apps/api/src/Api/Program.cs`

Add HyperDX exporter to existing OpenTelemetry configuration:

```csharp
// Existing OTel setup
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            // Existing Jaeger exporter (keep during Phase 1)
            .AddJaegerExporter(options =>
            {
                options.Endpoint = new Uri("http://meepleai-jaeger:14268/api/traces");
            })
            // NEW: HyperDX exporter
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://meepleai-hyperdx:4317");
                options.Protocol = OtlpExportProtocol.Grpc;
            });
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            // Existing Prometheus exporter (keep)
            .AddPrometheusExporter()
            // NEW: HyperDX exporter
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://meepleai-hyperdx:4317");
                options.Protocol = OtlpExportProtocol.Grpc;
            });
    });

// Existing Serilog → Seq (keep during Phase 1)
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .WriteTo.Seq("http://meepleai-seq:5341")
        // NEW: HyperDX via OTLP logs
        .WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = "http://meepleai-hyperdx:4318/v1/logs";
            options.Protocol = OtlpProtocol.HttpProtobuf;
        });
});
```

**NuGet Packages** (add if missing):
```bash
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol --version 1.7.0
dotnet add package Serilog.Sinks.OpenTelemetry --version 1.0.0
```

---

#### Step 3: Configure Next.js Frontend (Browser)

**File**: `apps/web/package.json`

Add HyperDX browser SDK:

```bash
cd apps/web
pnpm add @hyperdx/browser
```

**File**: `apps/web/src/app/layout.tsx`

Initialize HyperDX before other providers:

```tsx
'use client';

import { HyperDX } from '@hyperdx/browser';
import { useEffect } from 'react';

// Initialize HyperDX on client-side
if (typeof window !== 'undefined') {
  HyperDX.init({
    apiKey: process.env.NEXT_PUBLIC_HYPERDX_API_KEY || 'demo',
    service: 'meepleai-web',
    tracePropagationTargets: [/localhost:5080/, /api\.meepleai\.dev/],
    consoleCapture: true,
    advancedNetworkCapture: true,
  });
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
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

**File**: `apps/web/.env.example`

Add HyperDX configuration:

```bash
# HyperDX Observability
NEXT_PUBLIC_HYPERDX_API_KEY=demo
HYPERDX_ENDPOINT=http://localhost:4318
```

---

#### Step 4: Configure OpenTelemetry Context Propagation

**Backend** (`apps/api/src/Api/Program.cs`):

Ensure trace context is propagated to frontend responses:

```csharp
app.Use(async (context, next) =>
{
    // Add trace ID to response headers for frontend correlation
    var activity = Activity.Current;
    if (activity != null)
    {
        context.Response.Headers.Add("X-Trace-Id", activity.TraceId.ToString());
    }
    await next();
});
```

**Frontend** (`apps/web/src/lib/api/core/httpClient.ts`):

Read trace ID from response and attach to HyperDX session:

```typescript
import { HyperDX } from '@hyperdx/browser';

async function fetchWithTracing(url: string, options: RequestInit) {
  const response = await fetch(url, options);

  // Correlate frontend session with backend trace
  const traceId = response.headers.get('X-Trace-Id');
  if (traceId) {
    HyperDX.setGlobalAttributes({ backendTraceId: traceId });
  }

  return response;
}
```

---

### Phase 1 Validation & Metrics

**Week 1-2 Metrics to Track**:

| Metric | Baseline (Current) | Target (HyperDX) | Measurement |
|--------|-------------------|------------------|-------------|
| **Log Search Time (P95)** | 3-5s (Seq) | <1s | HyperDX UI query time |
| **Trace Correlation** | Manual (copy trace ID) | Automatic | Click logs → auto-open trace |
| **Session Replay Coverage** | 0% | 80% | % of user sessions recorded |
| **Alert Latency** | 2-3 min | <1 min | Time from error → alert fired |
| **MTTR (Mean Time To Resolution)** | 15 min avg | 10 min avg | Incident tickets |
| **Resource Usage (HyperDX)** | N/A | <4GB RAM, <2 CPU | Docker stats |
| **Data Retention** | 30 days | 30 days | ClickHouse disk usage |

**Daily Checks**:
- [ ] HyperDX ingesting all logs (compare count with Seq)
- [ ] HyperDX ingesting all traces (compare count with Jaeger)
- [ ] Session replay recording frontend interactions
- [ ] No missing telemetry data (check OTel exporter errors)

**Week 2 Go/No-Go Decision**:
- ✅ **GO to Phase 2** if: All success criteria met + team feedback positive
- ❌ **ABORT** if: Data loss, performance issues, or negative team feedback

---

### Phase 2 Implementation Steps

#### Step 1: Update Documentation

**Files to Update**:
- `docs/05-operations/README.md` → Add HyperDX as primary logs/traces tool
- `docs/05-operations/runbooks/*.md` → Replace Seq/Jaeger links with HyperDX
- `CLAUDE.md` → Update observability section

**Example**:
```markdown
## Observability (Updated 2025-11-21)

**Primary Tools**:
- **HyperDX** (http://localhost:8080): Logs, Traces, Session Replay
- **Prometheus** (http://localhost:9090): Metrics
- **Grafana** (http://localhost:3001): Dashboards

**Deprecated** (as of Phase 2):
- ~~Seq~~ → Use HyperDX for logs
- ~~Jaeger~~ → Use HyperDX for traces
```

---

#### Step 2: Decommission Seq + Jaeger

**Stop Services**:
```bash
cd infra
docker compose stop meepleai-seq meepleai-jaeger
docker compose rm -f meepleai-seq meepleai-jaeger
```

**Update `docker-compose.yml`**:
Comment out or remove Seq + Jaeger service definitions.

**Remove Exporters** (apps/api/src/Api/Program.cs):
```csharp
// REMOVE Jaeger exporter
// .AddJaegerExporter(...)

// REMOVE Seq sink
// .WriteTo.Seq("http://meepleai-seq:5341")
```

**Verify**:
- [ ] HyperDX still receiving all telemetry
- [ ] No errors in application logs about missing exporters
- [ ] Team can debug issues using HyperDX only

---

#### Step 3: Configure HyperDX Alerts

**Create Alerts in HyperDX UI** (http://localhost:8080/alerts):

1. **High Error Rate** (replace Alertmanager rule)
   - Condition: `error.count > 100` in 5 minutes
   - Channel: Email (badsworm@gmail.com)
   - Severity: Critical

2. **Slow API Response** (new capability)
   - Condition: `http.server.duration > 3000ms` (P95)
   - Channel: Slack (#alerts)
   - Severity: Warning

3. **Frontend Error Spike** (new capability)
   - Condition: `browser.error.count > 50` in 5 minutes
   - Channel: Email + Slack
   - Severity: Critical

**Keep in Alertmanager** (critical infrastructure):
- DatabaseDown
- RedisDown
- QdrantDown
- HighMemoryUsage
- DiskSpacelow

---

### Phase 3 Decision Matrix

**Evaluation Criteria** (Week 5):

| Criterion | Weight | HyperDX Score | Grafana Stack Score | Winner |
|-----------|--------|---------------|---------------------|--------|
| **Search Performance** | 20% | ⭐⭐⭐⭐⭐ (ClickHouse) | ⭐⭐⭐ (Seq) | HyperDX |
| **Operational Complexity** | 25% | ⭐⭐⭐⭐⭐ (1 service) | ⭐⭐ (5 services) | HyperDX |
| **Cost (Self-Hosted)** | 15% | ⭐⭐⭐⭐ ($0.40/GB) | ⭐⭐⭐⭐⭐ (Free) | Grafana |
| **Feature Completeness** | 20% | ⭐⭐⭐⭐⭐ (Session Replay) | ⭐⭐⭐ (No Session Replay) | HyperDX |
| **Team Productivity** | 10% | TBD (measure MTTR) | Baseline | TBD |
| **Community & Support** | 10% | ⭐⭐⭐ (YC-backed) | ⭐⭐⭐⭐⭐ (Large ecosystem) | Grafana |

**Final Architecture Options**:

**Option A: Hybrid (Recommended)**
```
Observability Stack:
├── HyperDX (Logs, Traces, Session Replay, App Alerts)
├── Prometheus (Infrastructure Metrics)
├── Grafana (Business Dashboards)
└── Alertmanager (Critical Infrastructure Alerts)

Benefits: Best of both worlds, smooth migration
Drawbacks: Still 4 services to maintain
```

**Option B: Full HyperDX**
```
Observability Stack:
└── HyperDX (All telemetry + dashboards + alerts)

Benefits: Simplest architecture, lowest operational overhead
Drawbacks: Loss of Grafana ecosystem, less community support
```

**Option C: Revert to Grafana Stack**
```
Observability Stack:
├── Seq (Logs)
├── Jaeger (Traces)
├── Prometheus (Metrics)
├── Grafana (Dashboards)
└── Alertmanager (Alerts)

Benefits: Proven stack, large community
Drawbacks: No session replay, high operational overhead
```

---

## Rollback Plan

### Phase 1 Rollback (Weeks 1-2)

**If HyperDX doesn't meet expectations**:

1. Stop HyperDX container:
   ```bash
   docker compose -f docker-compose.hyperdx.yml down
   ```

2. Remove HyperDX exporters from code:
   ```bash
   git revert <commit-sha>  # Revert OTel exporter changes
   ```

3. Remove HyperDX browser SDK:
   ```bash
   pnpm remove @hyperdx/browser
   ```

**Impact**: Zero - existing Seq + Jaeger still running.

---

### Phase 2 Rollback (Weeks 3-4)

**If HyperDX fails in production**:

1. Restart Seq + Jaeger:
   ```bash
   docker compose up -d meepleai-seq meepleai-jaeger
   ```

2. Re-add exporters to code:
   ```bash
   git revert <commit-sha>  # Revert Seq/Jaeger removal
   ```

3. Update documentation:
   ```bash
   git revert <doc-commit-sha>
   ```

**Estimated Rollback Time**: 10 minutes
**Impact**: Brief gap in telemetry data during switch (≤5 minutes)

---

## Testing & Validation

### Pre-Deployment Tests

**Infrastructure Tests**:
- [ ] HyperDX container starts successfully
- [ ] Ports 8080, 4317, 4318 accessible
- [ ] ClickHouse data persisted across restarts
- [ ] Health check returns HTTP 200

**Integration Tests**:
- [ ] .NET API sends traces to HyperDX
- [ ] .NET API sends metrics to HyperDX
- [ ] Serilog logs appear in HyperDX
- [ ] Next.js app sends browser events to HyperDX
- [ ] Session replay captures user interactions

**Performance Tests**:
- [ ] HyperDX handles 1000 logs/sec (load test with k6)
- [ ] Search queries return in <1s for 10K logs
- [ ] Trace queries return in <500ms for 1K traces
- [ ] Session replay playback smooth (no lag)

---

### Post-Deployment Validation

**Week 1 Checkpoints**:
- [ ] Day 1: Verify all telemetry ingested (100% coverage)
- [ ] Day 3: Test manual log correlation → trace → session replay
- [ ] Day 5: Review team feedback (survey developers)
- [ ] Day 7: Analyze resource usage (CPU, RAM, disk)

**Week 2 Checkpoints**:
- [ ] Day 10: Trigger test alert, verify email/Slack notification
- [ ] Day 12: Simulate production incident, measure MTTR
- [ ] Day 14: Go/No-Go decision meeting

**Metrics Dashboard** (Grafana):
Create temporary dashboard to compare HyperDX vs Seq/Jaeger:
- Log ingestion rate (logs/sec)
- Trace ingestion rate (traces/sec)
- Query latency (P50, P95, P99)
- Alert latency (time from event to notification)
- Resource usage (CPU, RAM, disk I/O)

---

## Risk Assessment

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| **Data Loss During Migration** | Low | High | Run dual-export during Phase 1, keep Seq/Jaeger as backup |
| **Performance Degradation** | Medium | Medium | Load test HyperDX before production, set resource limits |
| **Team Resistance** | Medium | Low | Involve team in evaluation, provide training |
| **ClickHouse Storage Cost** | Low | Medium | Monitor disk usage, set retention policies (30 days) |
| **Vendor Lock-In** | Low | High | Use OpenTelemetry standard, avoid HyperDX-specific APIs |
| **Missing Features** | Medium | Medium | Identify gaps early in Phase 1, keep Grafana as fallback |

---

## Cost Analysis

### Self-Hosted HyperDX Costs

**Infrastructure** (Docker host):
- **Compute**: 2 CPU cores, 4GB RAM → ~$20/month (DigitalOcean Droplet)
- **Storage**: 50GB SSD (ClickHouse data) → ~$5/month
- **Network**: 1TB transfer → Included

**Total**: ~$25/month (vs $0 for Grafana stack)

**Cost Comparison** (if cloud-hosted):
- **HyperDX Cloud**: $0.40/GB × 100GB/month = $40/month
- **Datadog**: $15/host + $0.10/GB × 100GB = $25/month (10x more)
- **New Relic**: $0.30/GB × 100GB = $30/month (3x more)

**Break-Even**: HyperDX cloud cheaper than Datadog/New Relic if >10GB/month ingested.

---

## Timeline

### Gantt Chart (6 Weeks)

```
Week 1-2: Phase 1 - Evaluation
├── Week 1:
│   ├── Day 1-2: Deploy HyperDX, configure .NET API
│   ├── Day 3-4: Configure Next.js frontend
│   └── Day 5-7: Monitor dual-export, collect metrics
└── Week 2:
    ├── Day 8-10: Analyze metrics, team survey
    ├── Day 11-13: Performance testing, alert testing
    └── Day 14: Go/No-Go decision

Week 3-4: Phase 2 - Partial Replacement
├── Week 3:
│   ├── Day 15-16: Stop Seq/Jaeger, update docs
│   ├── Day 17-18: Configure HyperDX alerts
│   └── Day 19-21: Monitor production stability
└── Week 4:
    ├── Day 22-24: Team training on HyperDX
    ├── Day 25-27: Optimize dashboards, alerts
    └── Day 28: Phase 2 retrospective

Week 5-6: Phase 3 - Decision & Optimization
├── Week 5:
│   ├── Day 29-31: Evaluate metrics, team feedback
│   ├── Day 32-33: Final architecture decision
│   └── Day 34-35: Implementation (if changes needed)
└── Week 6:
    ├── Day 36-38: Documentation updates
    ├── Day 39-40: Runbook updates
    └── Day 41-42: Final validation, project closeout
```

---

## Success Metrics

### Quantitative Metrics

**Performance**:
- ✅ Log search time: <1s P95 (vs 3-5s baseline)
- ✅ Trace correlation: Automatic (vs manual copy-paste)
- ✅ Alert latency: <1 min (vs 2-3 min baseline)
- ✅ MTTR: 20% reduction (12 min vs 15 min baseline)

**Operational**:
- ✅ Service count: 4 services (vs 5 baseline)
- ✅ Resource usage: <4GB RAM (HyperDX only)
- ✅ Data retention: 30 days (same as baseline)
- ✅ Uptime: 99.9% (same as baseline)

### Qualitative Metrics

**Team Productivity**:
- ✅ Developer survey: ≥80% prefer HyperDX over Seq/Jaeger
- ✅ Onboarding time: New engineers productive in <1 day (vs 2 days)
- ✅ Debugging confidence: ≥90% feel confident debugging with HyperDX

**Business Impact**:
- ✅ Customer-impacting incidents: Zero incidents missed by HyperDX
- ✅ False positive alerts: <10% false positive rate
- ✅ Mean Time To Detect (MTTD): <5 min for critical issues

---

## Documentation Updates

### Files to Update

**After Phase 1**:
- [ ] `docs/05-operations/README.md` → Add HyperDX as evaluation tool
- [ ] `infra/docker-compose.hyperdx.yml` → Add HyperDX service
- [ ] `apps/web/.env.example` → Add HyperDX API key
- [ ] `CLAUDE.md` → Mention HyperDX in observability section

**After Phase 2**:
- [ ] `docs/05-operations/README.md` → Mark Seq/Jaeger as deprecated
- [ ] `docs/05-operations/runbooks/*.md` → Replace Seq/Jaeger links
- [ ] `apps/api/src/Api/Program.cs` → Remove Seq/Jaeger exporters
- [ ] `CLAUDE.md` → Update observability stack architecture

**After Phase 3**:
- [ ] `docs/01-architecture/diagrams/infrastructure-overview.md` → Update diagram
- [ ] `docs/05-operations/deployment/board-game-ai-deployment-guide.md` → Add HyperDX deployment
- [ ] ADR: Create `adr-015-hyperdx-observability.md` documenting decision

---

## Appendix

### A. HyperDX Docker Compose Reference

**Full Production Configuration**:

```yaml
# infra/docker-compose.hyperdx.yml
version: '3.8'

services:
  meepleai-hyperdx:
    image: docker.hyperdx.io/hyperdx/hyperdx-local:2-beta
    container_name: meepleai-hyperdx
    restart: unless-stopped
    ports:
      - "8080:8080"   # Web UI
      - "4317:4317"   # OTLP gRPC
      - "4318:4318"   # OTLP HTTP
      - "8123:8123"   # ClickHouse HTTP (for backups)
    volumes:
      - hyperdx-data:/var/lib/clickhouse
      - hyperdx-logs:/var/log/hyperdx
    environment:
      # Core Settings
      - HYPERDX_API_KEY=${HYPERDX_API_KEY:-demo}
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
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 60s

    networks:
      - meepleai

    # Resource Limits (prevent runaway usage)
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
    external: true
```

---

### B. OpenTelemetry Configuration Reference

**apps/api/src/Api/Program.cs** (Complete Example):

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
                options.EnrichWithIDbCommand = (activity, command) =>
                {
                    activity.SetTag("db.query", command.CommandText);
                };
            })

            // Exporters
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://meepleai-hyperdx:4317");
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
            .AddPrometheusExporter() // Keep for Grafana
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://meepleai-hyperdx:4317");
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

        // HyperDX via OTLP
        .WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = "http://meepleai-hyperdx:4318/v1/logs";
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
        context.Response.Headers.Add("X-Trace-Id", activity.TraceId.ToString());
        context.Response.Headers.Add("X-Span-Id", activity.SpanId.ToString());
    }
    await next();
});

app.Run();
```

---

### C. Next.js HyperDX Configuration Reference

**apps/web/src/lib/hyperdx.ts** (Initialization):

```typescript
// HyperDX Browser SDK Configuration
import { HyperDX } from '@hyperdx/browser';

const isProduction = process.env.NODE_ENV === 'production';
const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:5080';

export function initializeHyperDX() {
  if (typeof window === 'undefined') {
    return; // Server-side, do nothing
  }

  HyperDX.init({
    apiKey: process.env.NEXT_PUBLIC_HYPERDX_API_KEY || 'demo',
    service: 'meepleai-web',

    // Trace Propagation (correlate frontend → backend)
    tracePropagationTargets: [
      /localhost:5080/,
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

**apps/web/src/app/layout.tsx** (Usage):

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

### D. Alert Configuration Examples

**HyperDX UI → Alerts → Create Alert**:

#### Alert 1: High Error Rate (Critical)

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
  "message": "🚨 High error rate detected in API: {{count}} errors in {{window}}"
}
```

#### Alert 2: Slow API Response (Warning)

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

#### Alert 3: Frontend Error Spike (Critical)

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
    },
    {
      "type": "slack",
      "webhook": "${SLACK_WEBHOOK_URL}",
      "channel": "#alerts"
    }
  ],
  "severity": "critical",
  "message": "🚨 Frontend error spike: {{count}} errors in {{window}}\nCheck: http://localhost:8080/sessions"
}
```

---

### E. Retention & Storage Management

**ClickHouse Retention Policy** (HyperDX automatically applies):

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

**Storage Monitoring**:

```bash
# Check ClickHouse disk usage
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

# Expected output:
# database | table    | size
# hyperdx  | logs     | 15.2 GB
# hyperdx  | traces   | 8.4 GB
# hyperdx  | metrics  | 2.1 GB
# hyperdx  | sessions | 24.3 GB (if session replay enabled)
```

---

## References

### Official Documentation

- **HyperDX Docs**: https://www.hyperdx.io/docs
- **HyperDX GitHub**: https://github.com/hyperdxio/hyperdx
- **OpenTelemetry .NET**: https://opentelemetry.io/docs/languages/net/
- **ClickHouse Docs**: https://clickhouse.com/docs

### Internal Documentation

- **OPS-05 Setup**: `docs/05-operations/ops-05-setup.md`
- **Observability README**: `docs/05-operations/README.md`
- **Infrastructure Diagram**: `docs/01-architecture/diagrams/infrastructure-overview.md`
- **CLAUDE.md**: Project overview and stack

### Related Issues

- **#295**: Error Monitoring & Alerting (OPS-05) - Implemented Grafana-Native Stack
- **TBD**: HyperDX Migration Tracking Issue (create when Phase 1 starts)

---

**Document Status**: Draft v1.0
**Last Updated**: 2025-11-21
**Next Review**: After Phase 1 completion (Week 3)
