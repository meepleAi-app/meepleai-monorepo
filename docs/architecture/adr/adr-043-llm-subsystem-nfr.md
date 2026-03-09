# ADR-043: LLM Subsystem Non-Functional Requirements

**Status**: Accepted
**Date**: 2026-03-09
**Issue**: #5478 (Epic #5475: LLM Operational Maturity)
**Decision Makers**: Engineering Lead
**Supersedes**: Informal targets scattered across ADR-007, PRD, and code comments

---

## Context

The MeepleAI LLM subsystem (HybridLlmService + circuit breaker + tier-based routing) has been operating without formally documented non-functional requirements. Performance targets exist informally in ADR-007 and Prometheus alert thresholds, but there is no single source of truth for:

- Response time SLAs by provider
- Cost ceilings and budget enforcement
- Availability targets and measurement methodology
- Data retention policies
- Error budget allocation

This ADR establishes baseline NFR targets. These are **initial targets** — they should be validated against production metrics over a 30-day measurement period and adjusted based on observed behavior.

### Existing Infrastructure

The following monitoring infrastructure is already in place:

| Component | Reference |
|-----------|-----------|
| Prometheus recording rules | `infra/prometheus-rules.yml` (SLO burn rate alerts) |
| Custom metrics | `MeepleAiMetrics.cs` (14 metric instruments) |
| Circuit breaker | 3-state per-provider, 5 failures → open, 30s recovery |
| Rate limiting | Redis-backed RPD (FreeModelQuotaTracker) + RPM/TPM (OpenRouterRateLimitTracker) |
| Cost tracking | `LlmCostCalculator` with per-model pricing, DB persistence |
| Admin dashboards | 40+ pages including debug console, usage monitoring |

---

## Decision

Establish the following non-functional requirements for the LLM subsystem.

### 1. Response Time

| Metric | Ollama (local) | OpenRouter (cloud) | Measurement |
|--------|---------------|-------------------|-------------|
| P50 latency | ≤ 800 ms | ≤ 1,200 ms | `gen_ai.client.operation.duration` by provider |
| P95 latency | ≤ 2,000 ms | ≤ 3,000 ms | `gen_ai.client.operation.duration` by provider |
| P99 latency | ≤ 4,000 ms | ≤ 6,000 ms | `gen_ai.client.operation.duration` by provider |
| Hard timeout | 30 s | 30 s | `HttpClient.Timeout` configuration |
| TTFT (Time-To-First-Token) | ≤ 500 ms (P95) | ≤ 800 ms (P95) | `meepleai.rag.first_token_latency` |

**Notes**:
- Latency measured from request dispatch to last token received (full response).
- TTFT is the critical user-perceived metric — the SLO burn rate alert (`SloRagTtftFastBurn`) already targets P95 < 800ms.
- These targets apply to the LLM call itself, excluding embedding + vector search. End-to-end RAG response time has a separate SLO (P95 < 3s, see `prometheus-rules.yml`).

### 2. Cost Ceilings

| Threshold | Limit | Action | Enforcement |
|-----------|-------|--------|-------------|
| Daily soft limit | $50 | Log warning + admin alert | `LlmCostCalculator` daily aggregation |
| Daily hard limit | $75 | Auto-switch to Ollama-only mode | Admin Emergency Controls (#5476) |
| Weekly projection alert | $500 | Admin notification | Prometheus alert rule |
| Monthly hard limit | $1,500 | Auto-switch to Ollama-only + email alert | Admin Emergency Controls (#5476) |

**Cost tracking metrics**:
- Per-invocation: `meepleai.llm.cost.usd` (tags: `model`, `provider`)
- Per-agent: `meepleai.agent.cost.usd` (tags: `agent_type`)
- Token usage: `gen_ai.client.token.usage` (tags: `token_type`, `model_id`, `provider`)

**Budget enforcement flow**:
1. `LlmCostCalculator` tracks real-time cost per invocation
2. Daily aggregation checked before each OpenRouter call
3. At $50/day: warning logged, admin notified
4. At $75/day: automatic Ollama-only override (via emergency controls, #5476)
5. Monthly roll-up checked weekly; projection alert at 80% of limit

### 3. Availability

| Target | Value | Measurement | Error Budget (28-day) |
|--------|-------|-------------|----------------------|
| LLM subsystem availability | 99.5% | Successful LLM responses / total LLM requests | 3h 21m downtime |
| End-to-end RAG availability | 99.0% | Non-5xx HTTP responses / total requests | 6h 43m downtime |

**Availability definition**:
- A request is **successful** if it returns a valid LLM response (including fallback responses).
- A request **fails** if it results in a timeout, circuit breaker rejection, or unhandled exception.
- **Fallback to Ollama counts as available** — degraded quality is acceptable; total failure is not.
- Provider-specific outages do not count against availability if fallback succeeds.

**Existing SLO alerts** (from `prometheus-rules.yml`):
- `SloAvailabilityFastBurn`: 5xx ratio > 0.5% in 1h window
- `SloAvailabilitySlowBurn`: 5xx ratio > 0.8% in 6h window

### 4. Data Retention

| Data Type | Retention | Storage | Justification |
|-----------|-----------|---------|--------------|
| LLM request/response logs | 30 days | PostgreSQL (`llm_cost_logs`) | Debugging, cost attribution |
| Conversation history | 90 days | PostgreSQL (`chat_messages`) | User experience continuity |
| Aggregated analytics | 1 year | PostgreSQL (materialized views) | Trend analysis, capacity planning |
| Prometheus metrics (raw) | 15 days | Prometheus TSDB | Real-time monitoring |
| Prometheus metrics (downsampled) | 90 days | Prometheus TSDB (recording rules) | SLO tracking |

**GDPR considerations**:
- Request logs containing user content must be anonymizable on request.
- Conversation history deletion is covered by user account deletion flow.
- Aggregated analytics must not contain PII.

### 5. Resilience Requirements

| Scenario | Expected Behavior | Recovery Time |
|----------|-------------------|---------------|
| Ollama container crash | Route 100% to OpenRouter | < 30s (circuit breaker open) |
| OpenRouter API down | Route 100% to Ollama | < 30s (circuit breaker open) |
| Both providers down | Return error with retry-after header | N/A — total failure |
| Redis unavailable | Continue without rate limiting (fail-open) | Auto-recover on reconnect |
| High token cost spike | Admin alert at $50, auto-Ollama at $75 | Immediate |
| Circuit breaker flapping | Exponential backoff on half-open probes | 30s → 60s → 120s |

### 6. Capacity

| Dimension | Current | Target | Scaling Path |
|-----------|---------|--------|-------------|
| Concurrent LLM requests | ~10 (Ollama) | 20 | GPU scaling / model quantization |
| OpenRouter RPM | 60 (free tier) | 200 (paid tier) | Tier upgrade |
| Free tier daily quota | 100 RPD | 100 RPD | Configurable via `FreeModelQuotaTracker` |
| Vector search latency | < 50ms | < 100ms at 1M docs | Qdrant horizontal scaling |

---

## Measurement Phase

> **Important**: These targets are initial baselines. Before enforcing hard limits, a 30-day measurement phase is required.

### Phase 1: Measure (Days 1-30)
1. Deploy Prometheus metrics export (#5480) to capture provider-level latency
2. Collect P50/P95/P99 baselines per provider
3. Document actual daily cost distribution
4. Measure real availability (successful responses / total)

### Phase 2: Validate (Days 31-45)
1. Compare actuals vs targets in this ADR
2. Adjust targets if actuals differ significantly (>20% deviation)
3. Update Prometheus alert thresholds to match validated targets
4. Add cost ceiling enforcement (#5476)

### Phase 3: Enforce (Day 46+)
1. Enable hard limits (cost ceilings, auto-Ollama switch)
2. Activate SLO burn rate alerts for LLM-specific latency
3. Set up weekly SLO review cadence

---

## Alternatives Considered

### Option A: No Formal NFRs (Status Quo — Rejected)
- **Pros**: No overhead
- **Cons**: No accountability, no early warning, no cost control
- **Decision**: Unacceptable for production system with real costs

### Option B: Strict SLAs with Penalty Clauses (Rejected)
- **Pros**: Strong accountability
- **Cons**: Premature without baseline data; internal system (no external SLA needed)
- **Decision**: Overkill for current stage; revisit when serving external clients

### Option C: Tiered NFR Targets (Accepted)
- **Pros**: Realistic targets by provider, measurement-first approach
- **Cons**: More complex to track
- **Decision**: Best fit — acknowledges different provider characteristics

---

## Consequences

### Positive
- **Single source of truth** for LLM subsystem performance expectations
- **Cost protection** via automated daily/monthly limits
- **SLO-driven operations** with error budget tracking
- **Measurement-first approach** prevents setting unrealistic targets

### Negative
- **Maintenance burden**: Targets must be reviewed quarterly
- **Alert fatigue risk**: Too many alerts if thresholds are too tight
- **Cost enforcement complexity**: Requires Admin Emergency Controls (#5476) for full implementation

### Neutral
- **No code changes in this ADR** — documentation only; enforcement is in companion issues (#5476, #5480)
- **Complements existing infrastructure**: Builds on SLO recording rules (#5541) and MeepleAiMetrics

---

## Monitoring Dashboard Requirements

To show targets vs actuals, the following dashboard panels are needed:

| Panel | Metric Source | Target Line |
|-------|--------------|-------------|
| LLM Latency by Provider (P50/P95) | `gen_ai.client.operation.duration` | 800ms/2000ms (Ollama), 1200ms/3000ms (OpenRouter) |
| TTFT Distribution | `meepleai.rag.first_token_latency` | 800ms (P95) |
| Daily Cost | `meepleai.llm.cost.usd` (daily sum) | $50 warning, $75 hard limit |
| Monthly Cost Projection | `meepleai.llm.cost.usd` (30-day sum) | $1,500 |
| Availability (28-day rolling) | `meepleai:slo:availability:error_ratio:6h` | 99.5% |
| Error Budget Remaining | SLO burn rate rules | 100% → 0% |
| Circuit Breaker State | New metric (#5480) | N/A (state indicator) |

---

## References

- **ADR-007**: Hybrid LLM Architecture (routing, models, cost tracking)
- **Issue #5476**: Admin Emergency Controls API (cost ceiling enforcement)
- **Issue #5480**: Prometheus Metrics Export (metric instrumentation)
- **Issue #5541**: SLO Recording Rules (existing burn rate alerts)
- **PRD**: `docs/plans/2026-03-08-llm-system-improvement-prd.md`
- **MeepleAiMetrics.cs**: `apps/api/src/Api/Observability/MeepleAiMetrics.cs`
- **Prometheus rules**: `infra/prometheus-rules.yml`

---

**Version**: 1.0
**Last Updated**: 2026-03-09
**Owner**: Engineering Lead
