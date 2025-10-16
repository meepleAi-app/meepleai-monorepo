# OPS-02: OpenTelemetry Observability Stack - Design Document

**Issue**: #261
**Date**: 2025-10-16
**Status**: Design Phase

## Overview

This document outlines the design and architecture for implementing distributed tracing and metrics using OpenTelemetry in the MeepleAI monorepo.

## Current State (OPS-01)

**Existing Observability**:
- ✅ Serilog structured logging (Console + Seq)
- ✅ Correlation IDs (X-Correlation-Id header)
- ✅ Request/response logging with timing
- ✅ Health checks (Postgres, Redis, Qdrant)
- ✅ Seq dashboard (http://localhost:8081)

**Gaps**:
- ❌ No distributed tracing
- ❌ No metrics export (Prometheus)
- ❌ No trace visualization
- ❌ No performance dashboards

## Target State (OPS-02)

**New Capabilities**:
- ✅ Distributed tracing with OpenTelemetry
- ✅ OTLP export to Jaeger for trace visualization
- ✅ Prometheus metrics export
- ✅ Grafana dashboards for observability
- ✅ Integration with existing correlation IDs

## Technology Decisions

### 1. OpenTelemetry SDK Version

**Decision**: Use OpenTelemetry 1.13.1 (latest stable as of Oct 2025)

**Rationale**:
- Full .NET 9 support
- Stable and production-ready
- Supports Microsoft.Extensions.Diagnostics.Abstractions >= 9.0.0
- Active development and maintenance

### 2. Trace Backend: Jaeger vs Tempo

**Decision**: Start with **Jaeger** (all-in-one)

**Rationale**:
- Simpler setup (single Docker container)
- Built-in UI for trace visualization
- Lower resource requirements for dev/staging
- OTLP support (can migrate to Tempo later if needed)
- Faster development iteration

**Migration Path**: If we need better scalability/Grafana integration later, we can switch to Tempo without changing application code (OTLP is vendor-neutral).

### 3. Metrics Backend: Prometheus

**Decision**: Use Prometheus with Grafana

**Rationale**:
- Industry standard for metrics
- Native integration with Grafana
- Powerful query language (PromQL)
- Low resource overhead
- Well-documented

### 4. Export Protocol: OTLP

**Decision**: Use OTLP (OpenTelemetry Protocol) over HTTP

**Rationale**:
- Vendor-neutral (works with Jaeger, Tempo, Honeycomb, etc.)
- HTTP transport (firewall-friendly)
- Better error handling than legacy protocols
- Future-proof

## Architecture

### Component Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                      MeepleAI API (.NET 9)                   │
│  ┌────────────────────────────────────────────────────────┐ │
│  │           OpenTelemetry SDK (1.13.1)                   │ │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ │ │
│  │  │   Traces     │  │   Metrics    │  │   Logs       │ │ │
│  │  └──────────────┘  └──────────────┘  └──────────────┘ │ │
│  └────────────────────────────────────────────────────────┘ │
│           │                    │                    │        │
│           │ OTLP/HTTP         │ Prometheus         │        │
│           │ :4318             │ /metrics           │ Serilog│
└───────────┼────────────────────┼────────────────────┼────────┘
            │                    │                    │
            ▼                    ▼                    ▼
    ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
    │   Jaeger     │     │  Prometheus  │     │     Seq      │
    │  (traces)    │     │  (metrics)   │     │   (logs)     │
    │   :16686     │     │    :9090     │     │    :8081     │
    └──────────────┘     └──────────────┘     └──────────────┘
            │                    │
            └────────┬───────────┘
                     ▼
              ┌──────────────┐
              │   Grafana    │
              │ (dashboards) │
              │    :3001     │
              └──────────────┘
```

### Data Flow

1. **Traces**: API → OTLP Exporter → Jaeger :4318 → Jaeger UI :16686
2. **Metrics**: API → Prometheus Exporter → Prometheus scrapes /metrics :8080 → Grafana :3001
3. **Logs**: API → Serilog → Seq :5341 → Seq UI :8081 (existing)

### Correlation Strategy

**Trace ID = Correlation ID**:
- Use existing `X-Correlation-Id` header as trace ID
- Map `HttpContext.TraceIdentifier` to OpenTelemetry trace context
- Enrich Serilog logs with trace ID for cross-correlation
- Serilog logs can be linked to traces via trace ID

## Package Selection

### Core Packages (All 1.13.1)

```xml
<!-- OpenTelemetry Core -->
<PackageReference Include="OpenTelemetry" Version="1.13.1" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.13.1" />

<!-- Instrumentation - v1.12.0 (latest stable for instrumentation packages) -->
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.12.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.12.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.12.0" />

<!-- Exporters -->
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.13.1" />
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.13.1-beta.1" />
```

**Note**:
- Instrumentation packages (AspNetCore, Http, Runtime) are at v1.12.0 (latest stable)
- Prometheus exporter is at v1.13.1-beta.1 (no stable version available yet, but safe for use)

## Metrics Design

### Standard Metrics (Auto-instrumented)

**HTTP Metrics** (via AspNetCore instrumentation):
- `http.server.request.duration` - Request duration histogram
- `http.server.active_requests` - Active requests gauge
- `http.server.request.count` - Total requests counter

**Runtime Metrics** (via Runtime instrumentation):
- `process.runtime.dotnet.gc.collections.count` - GC collections
- `process.runtime.dotnet.gc.heap.size` - Heap size
- `process.runtime.dotnet.gc.pause.time` - GC pause time
- `process.runtime.dotnet.thread_pool.threads.count` - Thread pool size
- `process.runtime.dotnet.exceptions.count` - Exception count

**HTTP Client Metrics** (via Http instrumentation):
- `http.client.request.duration` - Outbound request duration
- `http.client.request.count` - Outbound request count

### Custom Metrics (Domain-specific)

**RAG/AI Metrics** (Meter: `MeepleAI.Rag`):
- `meepleai.rag.requests.total` - Total RAG requests (counter)
- `meepleai.rag.request.duration` - RAG request duration in ms (histogram)
- `meepleai.rag.tokens.used` - AI tokens used per request (histogram)
- `meepleai.rag.confidence.score` - Confidence score distribution (histogram)
- `meepleai.rag.errors.total` - RAG errors by type (counter)

**Vector Search Metrics** (Meter: `MeepleAI.VectorSearch`):
- `meepleai.vector.search.total` - Total vector searches (counter)
- `meepleai.vector.search.duration` - Search duration in ms (histogram)
- `meepleai.vector.results.count` - Number of results returned (histogram)
- `meepleai.vector.indexing.duration` - Indexing duration in ms (histogram)

**PDF Processing Metrics** (Meter: `MeepleAI.Pdf`):
- `meepleai.pdf.upload.total` - Total PDF uploads (counter)
- `meepleai.pdf.processing.duration` - Processing duration in ms (histogram)
- `meepleai.pdf.pages.processed` - Pages processed (counter)
- `meepleai.pdf.extraction.errors` - Extraction errors (counter)

**Cache Metrics** (Meter: `MeepleAI.Cache`):
- `meepleai.cache.hits.total` - Cache hits (counter)
- `meepleai.cache.misses.total` - Cache misses (counter)
- `meepleai.cache.evictions.total` - Cache evictions (counter)

## Trace Design

### Auto-instrumented Traces

**ASP.NET Core** (via AspNetCore instrumentation):
- HTTP request spans with method, path, status code
- Request enrichment: user agent, remote IP, user ID
- Exception recording

**HTTP Client** (via Http instrumentation):
- Outbound HTTP request spans
- Automatic propagation of trace context

**Entity Framework Core** (via EF Core instrumentation):
- Database query spans with SQL statements
- Connection and transaction spans

**Redis** (via StackExchange.Redis instrumentation):
- Redis command spans
- Connection pooling spans

### Custom Traces

**ActivitySource**: `MeepleAI.*` (one per service)

**Examples**:

1. **RagService** (`MeepleAI.RagService`):
   - `RagService.Ask` - Overall RAG request span
   - Tags: `game.id`, `query.length`, `response.tokens`, `response.confidence`

2. **QdrantService** (`MeepleAI.QdrantService`):
   - `QdrantService.Search` - Vector search span
   - `QdrantService.IndexTextChunks` - Indexing span
   - Tags: `collection.name`, `vector.dimension`, `results.count`

3. **PdfProcessingService** (`MeepleAI.PdfProcessing`):
   - `PdfProcessing.ExtractText` - Text extraction span
   - `PdfProcessing.ChunkText` - Chunking span
   - Tags: `pdf.pages`, `chunks.count`

### Trace Context Propagation

- **W3C Trace Context** (default): `traceparent` header
- **Correlation**: Map `X-Correlation-Id` to trace ID
- **Baggage**: Propagate user ID, tenant ID (if multi-tenant in future)

## Configuration

### Environment Variables

```bash
# OpenTelemetry Configuration
OTLP_ENDPOINT=http://jaeger:4318
OTEL_SERVICE_NAME=meepleai-api
OTEL_SERVICE_VERSION=1.0.0
OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
OTEL_TRACES_SAMPLER=always_on  # Dev: always_on, Prod: traceidratio
OTEL_TRACES_SAMPLER_ARG=1.0    # Sample 100% in dev, 0.1 (10%) in prod
```

### appsettings.json

```json
{
  "OpenTelemetry": {
    "ServiceName": "meepleai-api",
    "ServiceVersion": "1.0.0",
    "OtlpEndpoint": "http://jaeger:4318",
    "Traces": {
      "Enabled": true,
      "Sampler": "always_on",
      "SamplerArg": 1.0
    },
    "Metrics": {
      "Enabled": true,
      "PrometheusEndpoint": "/metrics"
    }
  }
}
```

## Grafana Dashboards

### Dashboard 1: API Performance

**Panels**:
- Request rate (req/s) - Time series
- Request duration (p50, p95, p99) - Time series
- Error rate (%) - Time series
- Status code distribution - Pie chart
- Active requests - Gauge
- Top 10 slowest endpoints - Table

**Metrics Used**:
- `http.server.request.duration`
- `http.server.request.count`
- `http.server.active_requests`

### Dashboard 2: AI/RAG Operations

**Panels**:
- RAG request rate - Time series
- RAG request duration (p50, p95, p99) - Time series
- Token usage over time - Time series
- Confidence score distribution - Histogram
- RAG errors by type - Bar chart
- Vector search latency - Time series

**Metrics Used**:
- `meepleai.rag.requests.total`
- `meepleai.rag.request.duration`
- `meepleai.rag.tokens.used`
- `meepleai.rag.confidence.score`
- `meepleai.vector.search.duration`

### Dashboard 3: Infrastructure

**Panels**:
- Database query duration - Time series
- Redis cache hit rate - Time series
- Redis command latency - Time series
- Memory usage (heap size) - Time series
- GC collections/sec - Time series
- Thread pool size - Time series
- Exception rate - Time series

**Metrics Used**:
- EF Core instrumentation metrics
- Redis instrumentation metrics
- `process.runtime.dotnet.*`

### Dashboard 4: Distributed Tracing (Jaeger integration)

**Panels**:
- Trace duration histogram - Histogram
- Service dependency graph - Graph
- Error traces - Table
- Slowest operations - Table

**Data Source**: Jaeger (via Grafana Tempo data source)

## Docker Services

### Jaeger (all-in-one)

```yaml
jaeger:
  image: jaegertracing/all-in-one:1.74.0
  restart: unless-stopped
  environment:
    COLLECTOR_OTLP_ENABLED: "true"
  ports:
    - "16686:16686"  # Jaeger UI
    - "4318:4318"    # OTLP HTTP receiver
  networks:
    - meepleai
  healthcheck:
    test: ["CMD", "wget", "--spider", "http://localhost:16686"]
    interval: 10s
    timeout: 5s
    retries: 5
```

### Prometheus

```yaml
prometheus:
  image: prom/prometheus:v3.7.0
  restart: unless-stopped
  command:
    - '--config.file=/etc/prometheus/prometheus.yml'
    - '--storage.tsdb.path=/prometheus'
    - '--storage.tsdb.retention.time=30d'
  volumes:
    - ./prometheus.yml:/etc/prometheus/prometheus.yml:ro
    - prometheusdata:/prometheus
  ports:
    - "9090:9090"
  networks:
    - meepleai
  healthcheck:
    test: ["CMD", "wget", "--spider", "http://localhost:9090/-/healthy"]
    interval: 10s
    timeout: 5s
    retries: 5
```

### Grafana

```yaml
grafana:
  image: grafana/grafana:11.4.0
  restart: unless-stopped
  environment:
    GF_SECURITY_ADMIN_USER: admin
    GF_SECURITY_ADMIN_PASSWORD: ${GRAFANA_PASSWORD:-admin}
    GF_USERS_ALLOW_SIGN_UP: "false"
  volumes:
    - grafanadata:/var/lib/grafana
    - ./grafana-datasources.yml:/etc/grafana/provisioning/datasources/datasources.yml:ro
    - ./grafana-dashboards.yml:/etc/grafana/provisioning/dashboards/dashboards.yml:ro
    - ./dashboards:/var/lib/grafana/dashboards:ro
  ports:
    - "3001:3000"
  depends_on:
    - prometheus
    - jaeger
  networks:
    - meepleai
  healthcheck:
    test: ["CMD", "wget", "--spider", "http://localhost:3000/api/health"]
    interval: 10s
    timeout: 5s
    retries: 5
```

## Testing Strategy

### Unit Tests

**OpenTelemetryConfigurationTests.cs**:
- Test OpenTelemetry service registration
- Test ActivitySource creation
- Test Meter creation
- Test configuration loading

### Integration Tests

**TracingIntegrationTests.cs**:
- Test trace propagation through HTTP pipeline
- Test correlation ID mapping to trace ID
- Test custom span creation in services
- Test exception recording in traces

**MetricsIntegrationTests.cs**:
- Test metrics endpoint (/metrics) availability
- Test custom metrics recording
- Test metric value accuracy
- Test metric labels/tags

### E2E Tests

**Observability E2E**:
- Trigger API request → verify trace in Jaeger
- Trigger RAG request → verify custom metrics in Prometheus
- Verify Grafana dashboards display data
- Verify correlation between logs (Seq) and traces (Jaeger)

**Coverage Target**: 90% for new code

## Migration Strategy

### Phase 1: Infrastructure (Day 1-3)
- Add Jaeger, Prometheus, Grafana to Docker Compose
- Create configuration files
- Verify services health checks

### Phase 2: SDK Integration (Day 4-7)
- Add NuGet packages
- Configure OpenTelemetry in Program.cs
- Add auto-instrumentation
- Deploy to dev environment

### Phase 3: Custom Instrumentation (Day 8-10)
- Add custom traces in services
- Add custom metrics
- Test trace propagation

### Phase 4: Dashboards (Day 11-12)
- Create Grafana dashboards
- Configure alerting rules
- Document dashboard usage

### Phase 5: Testing & Documentation (Day 13-17)
- Write comprehensive tests
- Update documentation
- CI/CD integration
- Team training

## Performance Considerations

### Overhead

**Expected overhead** (based on OpenTelemetry benchmarks):
- **Traces**: ~1-5% CPU overhead with 100% sampling
- **Metrics**: ~0.5-2% CPU overhead
- **Memory**: +10-50 MB for SDK and buffers

**Mitigation**:
- Use sampling in production (10% recommended)
- Configure batch export (default: 2048 items or 5s)
- Limit span attribute sizes
- Monitor exporter queue size

### Sampling Strategy

**Development**: `always_on` (100% sampling)
**Staging**: `traceidratio` (50% sampling)
**Production**: `traceidratio` (10% sampling)

**Rationale**: Capture all traces in dev for debugging, sample in production to reduce overhead while maintaining observability.

## Security Considerations

### Authentication

- **Jaeger UI**: No auth by default (only expose on internal network)
- **Prometheus**: No auth by default (scrape endpoint on internal network)
- **Grafana**: Basic auth (admin/password from env var)

### Data Sensitivity

**PII in Traces**:
- ❌ Do NOT include passwords, API keys, tokens in span attributes
- ⚠️ Sanitize SQL queries (redact sensitive WHERE clauses)
- ✅ Include user IDs (non-sensitive identifiers)
- ✅ Include correlation IDs, request IDs

**Compliance**:
- Trace retention: 7 days (configurable in Jaeger)
- Metric retention: 30 days (configurable in Prometheus)
- Log retention: 30 days (configured in Seq - OPS-01)

### Network Security

- All observability services on internal Docker network
- Only expose UIs via reverse proxy in production
- Use TLS for OTLP export in production (http://jaeger:4318 → https://jaeger:4318)

## Success Criteria

1. ✅ OpenTelemetry SDK integrated and verified
2. ✅ Traces visible in Jaeger UI with correlation IDs (Issue #433 resolved)
3. ✅ Metrics available at /metrics endpoint
4. ✅ Prometheus scraping metrics successfully
5. ✅ Grafana dashboards displaying real-time data
6. ✅ Custom traces for RAG/AI operations (RagService, QdrantService)
7. ✅ Custom metrics for domain-specific operations
8. ✅ 90% test coverage for new code (20+ tracing tests)
9. ✅ Documentation complete and reviewed
10. ✅ CI/CD pipeline passing

## Implementation Status (2025-10-16)

### ✅ Completed (Issue #433 Resolution)

1. **Activity Sources Created**: `MeepleAiActivitySources` class with 5 domain-specific sources
   - `MeepleAI.Api` - Main application traces
   - `MeepleAI.Rag` - RAG operation traces
   - `MeepleAI.VectorSearch` - Qdrant vector search traces
   - `MeepleAI.PdfProcessing` - PDF processing traces
   - `MeepleAI.Cache` - Cache operation traces

2. **Tracing Configuration Fixed** (`Program.cs:324-353`):
   - Removed incorrect `MeepleAiMetrics.MeterName` from tracing config
   - Added explicit `Microsoft.AspNetCore` and `System.Net.Http` sources
   - Added all custom MeepleAI Activity Sources
   - Set `AlwaysOnSampler` for development (100% sampling)

3. **Services Instrumented**:
   - `RagService.AskAsync()` - Tags: game.id, query.length, operation, response.tokens, confidence, snippets.count
   - `RagService.ExplainAsync()` - Tags: game.id, topic.length, sections.count, citations.count, estimated.minutes
   - `QdrantService.IndexDocumentChunksAsync()` - Tags: game.id, pdf.id, chunks.count, collection, indexed.count
   - `QdrantService.SearchAsync()` - Tags: game.id, limit, collection, vector.dimension, results.count, top.score

4. **Testing**:
   - 15 unit tests in `OpenTelemetryTracingTests.cs`
   - 5 integration tests in `OpenTelemetryIntegrationTests.cs`
   - Total: 20+ tests covering Activity Sources, tag setting, exception handling, trace filtering

5. **Documentation**:
   - `docs/issue/ops-02-jaeger-tracing-fix.md` - Complete resolution documentation
   - `docs/ops-02-opentelemetry-design.md` - Updated with implementation status

### Key Lesson Learned (Issue #433)

**Problem**: Traces not appearing in Jaeger despite correct infrastructure setup.

**Root Cause**: Confusion between `Meter` (for metrics) and `ActivitySource` (for tracing).

**Original Code** (incorrect):
```csharp
.WithTracing(tracing => tracing
    .AddSource(MeepleAiMetrics.MeterName))  // ❌ Meter, not ActivitySource
```

**Fixed Code**:
```csharp
.WithTracing(tracing => tracing
    .AddSource("Microsoft.AspNetCore")  // ✅ ActivitySource for framework traces
    .AddSource(MeepleAiActivitySources.ApiSourceName)  // ✅ Custom ActivitySource
    .AddSource(MeepleAiActivitySources.RagSourceName))
```

**Distinction**:
- **Meter** = Record numeric measurements (counters, histograms) → Metrics
- **ActivitySource** = Record operation spans → Distributed Traces

See `docs/issue/ops-02-jaeger-tracing-fix.md` for complete resolution details.

## Risks & Mitigation

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Performance degradation | High | Low | Use sampling in prod, monitor overhead |
| OTLP exporter failures | Medium | Medium | Add circuit breaker, fallback to noop |
| Jaeger storage full | Medium | Low | Configure retention policies, monitor disk |
| Prometheus scraping errors | Low | Low | Add health check for /metrics endpoint |
| Dashboard complexity | Low | Medium | Start with simple dashboards, iterate |

## References

- OpenTelemetry .NET Docs: https://opentelemetry.io/docs/languages/dotnet/
- Microsoft .NET Observability: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel
- Jaeger Docs: https://www.jaegertracing.io/docs/
- Prometheus Docs: https://prometheus.io/docs/
- Grafana Docs: https://grafana.com/docs/

---

**Last Updated**: 2025-10-16
**Next Review**: After Phase 2 completion
