# ADR-040: HyperDX for Unified Observability

**Status**: Accepted
**Date**: 2025-12-06
**Deciders**: DevOps Team, Engineering Lead
**Epic**: #1561 - HyperDX Integration

## Context

MeepleAI requires comprehensive observability for logs, traces, and session replay before production launch. The previous observability stack consisted of:
- **Seq** (:8081) for application logging
- **Jaeger** (:16686) for distributed tracing
- **Prometheus** (:9090) for metrics
- **Grafana** (:3001) for dashboards

We evaluated multiple solutions to consolidate and enhance our observability capabilities:

**Options Considered**:
1. **Grafana-Native Stack**: Seq + Jaeger + Loki + Tempo + Grafana
2. **HyperDX**: Unified platform (logs + traces + session replay)
3. **Datadog**: Commercial SaaS platform

## Decision

**Chosen**: **HyperDX** for application observability (logs, traces, session replay)

**Retained**: Prometheus/Grafana/Alertmanager for infrastructure metrics

**Replacement**: Seq and Jaeger deprecated in favor of HyperDX unified platform

## Rationale

### Why HyperDX

1. **Unified Platform**: 1 tool vs 2 (Seq + Jaeger) - reduced operational overhead
2. **Session Replay**: Critical for debugging frontend issues (not available in Grafana stack)
3. **Full-Text Search**: ClickHouse-powered, <1s queries (vs LogQL complexity)
4. **Auto-Correlation**: Click log → auto-open trace (vs manual trace ID copy)
5. **Cost**: Self-hosted ~$25/month (vs $0 Grafana, but missing features)
6. **OpenTelemetry**: No vendor lock-in, standard protocol (OTLP)
7. **Developer Experience**: Unified UI, simpler workflows, faster debugging

### Why Not Grafana Stack

- Missing session replay (critical for frontend debugging)
- Manual correlation required (copy trace ID from logs → Jaeger UI)
- Higher operational complexity (2+ services: Seq + Jaeger)
- No automatic pattern recognition or log clustering
- Steeper learning curve (LogQL, TraceQL)

### Why Not Datadog

- Cost: $15/host + $0.10/GB = ~$250/month (10x more expensive)
- Vendor lock-in: proprietary agent and protocol
- Overkill for pre-production MVP
- Not self-hosted (data sovereignty concerns)

## Consequences

### Positive

- ✅ Unified observability experience for developers
- ✅ Session replay enables faster frontend debugging (5-10x reduction in MTTR)
- ✅ Automatic log-trace-session correlation
- ✅ Reduced service count (14 vs 15 observability services)
- ✅ Simplified onboarding (one tool to learn vs two)
- ✅ ClickHouse backend scales to 100M+ events/day
- ✅ OpenTelemetry ensures easy migration if HyperDX doesn't scale

### Negative

- ❌ Additional $25/month cost vs free Grafana stack (acceptable for MVP)
- ❌ Smaller community than Grafana (but Y Combinator + ClickHouse backing)
- ❌ New tool for team to learn (mitigated by better UX)
- ❌ Requires dedicated docker-compose file (docker-compose.hyperdx.yml)

### Neutral

- Hybrid architecture: HyperDX (app) + Grafana (infra) adds slight complexity
- OpenTelemetry ensures easy migration if HyperDX doesn't scale
- Self-hosted version requires maintenance (vs SaaS)

## Implementation

**Timeline**: 2 weeks (2025-11-21 to 2025-12-06)

### Phase 1: Deployment (Week 1)
- ✅ Docker Compose setup (docker-compose.hyperdx.yml)
- ✅ OpenTelemetry instrumentation (backend + frontend)
- ✅ Health checks and monitoring integration
- ✅ Environment variable configuration

### Phase 2: Testing & Documentation (Week 2)
- ✅ Load testing with k6 (Issue #1568)
- ✅ Alert configuration (Issue #1567)
- ✅ Browser SDK integration (Issue #1566)
- ✅ Backend telemetry testing (Issue #1565)
- ✅ Documentation migration (Issue #1569)

### Migration Strategy

**Parallel Run** (Week 1):
- Run Seq + Jaeger + HyperDX simultaneously
- Compare log/trace completeness
- Verify correlation accuracy

**Cutover** (Week 2):
- Disable Seq and Jaeger
- Update all documentation
- Remove deprecated services from docker-compose.yml

**Rollback Plan**:
- Keep Seq/Jaeger configurations in git
- Can re-enable in <5 minutes if issues arise
- OpenTelemetry allows switching backends without code changes

## Technical Details

### Architecture

```
Application Layer (ASP.NET Core + Next.js)
              ↓
OpenTelemetry SDK (OTLP Protocol)
              ↓
HyperDX Collector (:4318 OTLP HTTP)
              ↓
ClickHouse Database (logs, traces, sessions)
              ↓
HyperDX UI (:8180)
```

### Integration Points

**Backend (ASP.NET Core)**:
- Serilog → OTLP Exporter → HyperDX
- OpenTelemetry .NET SDK → HyperDX
- W3C Trace Context propagation
- Correlation IDs in all logs

**Frontend (Next.js)**:
- HyperDX Browser SDK
- Session Replay capture
- Frontend errors and performance metrics
- User session tracking

**Infrastructure**:
- Prometheus → Grafana (unchanged)
- Alertmanager → HyperDX webhooks (optional)
- Health checks → `/health` endpoint

### Performance Characteristics

**Expected Load** (MVP Phase):
- Logs: ~10K events/hour (~240K/day)
- Traces: ~5K spans/hour (~120K/day)
- Sessions: ~100 sessions/day

**HyperDX Capacity**:
- ClickHouse handles 100M+ events/day
- Query latency: <1s for 1M events
- Retention: 30 days (configurable)

### Security Considerations

- Self-hosted: Data never leaves infrastructure
- API key authentication for ingestion
- OTLP over HTTPS in production
- No PII in logs (masked by default)

## Monitoring & Success Metrics

**Operational Metrics**:
- MTTR (Mean Time To Resolution): Target <15min for P1 incidents
- Log ingestion latency: <1s from event to UI
- Trace completeness: >99% of spans captured
- Session replay capture rate: >90% of user sessions

**Adoption Metrics**:
- Developer usage: >80% of team uses HyperDX weekly
- Incident resolution: >50% of incidents use HyperDX for debugging
- Feedback: NPS >8/10 from engineering team

## Related Decisions

- **ADR-008**: Streaming CQRS Migration (observability integration)
- **ADR-009**: Centralized Error Handling (log formatting)
- **ADR-010**: Security Headers Middleware (security telemetry)

## References

- [HyperDX Implementation Plan](../../../docs/05-operations/migration/hyperdx-implementation-plan.md)
- [Issue #1561: HyperDX Epic](https://github.com/user/meepleai/issues/1561)
- [Issue #1569: Documentation Migration](https://github.com/user/meepleai/issues/1569)
- [HyperDX Official Docs](https://www.hyperdx.io/docs)
- [OpenTelemetry Specification](https://opentelemetry.io/docs/specs/otel/)

## Changelog

- **2025-12-06**: ADR created and accepted
- **2025-12-06**: Phase 2 complete, documentation migrated
