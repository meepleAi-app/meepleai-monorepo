# LLM System Improvement PRD

**Date**: 2026-03-08
**Status**: Draft
**Author**: Spec Panel (Nygard, Fowler, Newman, Wiegers, Crispin, Hightower, Godin, Adzic)
**Overall Quality Score**: 5.9/10 → Target: 8.0/10

## Executive Summary

Deep analysis of MeepleAI's LLM subsystem revealed 8 critical gaps across operational maturity, architecture quality, transparency, testing, compliance, and scalability. This PRD defines 7 epics with ~40 issues to address these gaps systematically.

The system's foundation is solid — HybridLlmService with circuit breaker, fallback chain, tier-based routing, and comprehensive admin dashboards. But operational levers are missing, configuration is fragmented across 4 layers, the service is a 1100+ LOC monolith, and GDPR compliance has significant gaps.

## Current Architecture Summary

### LLM Stack
- **HybridLlmService** (14 constructor params, 1100+ LOC): Routes between Ollama (local) and OpenRouter (cloud)
- **Circuit Breaker**: 3-state (Closed→Open→HalfOpen), 5 failures to open, 30s recovery
- **Routing**: User tier × RagStrategy → (Provider, Model), with budget overrides
- **Monitoring**: 6 background services, Redis-backed rate limiting, 30-day request logs
- **Admin**: 40+ pages — agent CRUD, pipeline explorer, debug console, strategy config, usage monitoring

### Quality Assessment (as-is)

| Dimension | Score | Target |
|-----------|-------|--------|
| Architecture Clarity | 7.5/10 | 8.5 |
| Operational Readiness | 5.5/10 | 8.0 |
| Configuration Management | 4.5/10 | 7.5 |
| Testability | 6.0/10 | 8.0 |
| Resilience | 7.0/10 | 8.5 |
| Admin Experience | 6.5/10 | 8.0 |
| Requirements Coverage | 4.0/10 | 7.0 |
| GDPR Compliance | 3.0/10 | 8.0 |
| **Overall** | **5.9/10** | **8.0** |

---

## Epic A: LLM Operational Maturity

**Goal**: Give admins operational levers for incident response + observability
**Priority**: 🔴 Critical
**Estimated Issues**: 5

### A1: Admin Emergency Controls API

**Problem**: Admin sees a problem in Debug Console but has no immediate remediation path — must file a ticket to change config and redeploy.

**Solution**: New admin endpoint for immediate operational actions.

```
POST /api/v1/admin/llm/emergency
Body: {
  "action": "force-ollama-only" | "reset-circuit-breaker" | "flush-quota-cache" | "pause-openrouter",
  "durationMinutes": 30,  // auto-revert
  "reason": "RPD exhausted, OpenRouter billing spike"
}
```

**Acceptance Criteria**:
- Admin can force Ollama-only mode (no OpenRouter traffic)
- Admin can reset circuit breaker state for a specific provider
- Admin can flush free model quota cache in Redis
- Admin can pause OpenRouter (all traffic → Ollama)
- All actions are audit-logged with admin ID, reason, timestamp
- Actions auto-revert after configurable duration (default 30min)
- Frontend: Emergency Controls panel in Strategy Config page

### A2: Redis Failure Detection & Alert

**Problem**: When Redis is unavailable, `FreeModelQuotaTracker` and `RateLimitTracker` return null/0 — silently disabling all rate limiting. OpenRouter bills accumulate unchecked.

**Solution**: Detect Redis failures and alert admins.

**Acceptance Criteria**:
- When Redis operations fail, publish `InfrastructureDegradedEvent`
- Admin dashboard shows "⚠️ Rate Limiting Disabled" banner
- Option: hard-fail mode that blocks OpenRouter when Redis is down
- Health endpoint includes Redis rate-limiting subsystem status
- Background service monitors Redis connectivity every 30s

### A3: LLM Subsystem NFR Documentation

**Problem**: No documented SLAs for response time, availability, cost ceilings.

**Solution**: Formal NFR specification.

```yaml
response_time:
  p50_target: 800ms (Ollama), 1200ms (OpenRouter)
  p95_target: 2000ms (Ollama), 3000ms (OpenRouter)
  timeout: 30s (hard kill)
cost_ceiling:
  daily_hard_limit: $75 (auto-switch to Ollama-only)
  monthly_hard_limit: $1500 (auto-switch + admin alert)
availability:
  llm_subsystem: 99.5%
data_retention:
  request_logs: 30 days
  conversation_history: 90 days
  aggregated_analytics: 1 year
```

**Deliverable**: `docs/architecture/adr/adr-XXX-llm-nfr.md`

### A4: Prometheus Metrics Export from .NET

**Problem**: Prometheus + Grafana deployed but not scraping LLM-specific metrics. Admin dashboard is custom React polling REST endpoints — duplicates Grafana's native capability for operational alerting.

**Solution**: Export .NET metrics to Prometheus via `System.Diagnostics.Metrics`.

**Metrics to export**:
- `llm_requests_total{provider, model, status, source}`
- `llm_latency_seconds{provider, model}` (histogram)
- `circuit_breaker_state{provider}` (gauge: 0=closed, 1=open, 2=halfopen)
- `openrouter_balance_usd` (gauge)
- `openrouter_rpm_utilization` (gauge 0-1)
- `free_model_quota_remaining{model}` (gauge)
- `llm_cost_usd_total{provider, model}` (counter)

### A5: Chaos/Integration Test Suite for Resilience Paths

**Problem**: Unit tests strong, but no integration or chaos tests for critical failure paths.

**Solution**: Testcontainers-based chaos tests.

**Test scenarios**:
1. Kill Ollama mid-request → verify circuit opens → verify OpenRouter receives traffic
2. Exhaust OpenRouter RPD → verify Ollama fallback activates
3. Kill Redis → verify graceful degradation (no cost overruns) → verify alert fires
4. OpenRouter 429 burst → verify RPD tracking → verify Ollama fallback
5. All providers down → verify meaningful error to user (not 500)
6. Circuit breaker half-open → verify probe request → verify recovery

---

## Epic B: HybridLlmService Refactoring

**Goal**: Decompose the 14-param god service + unify configuration
**Priority**: 🟡 Important
**Estimated Issues**: 5

### B1: Extract ILlmProviderSelector

**Problem**: HybridLlmService mixes routing, circuit breaking, and quota checking with request orchestration.

**Solution**: Extract provider selection into dedicated service.

```csharp
public interface ILlmProviderSelector
{
    Task<LlmRoutingDecision> SelectProviderAsync(
        LlmRequest request,
        UserContext user,
        CancellationToken ct);
}
```

**Encapsulates**:
- `HybridAdaptiveRoutingStrategy` calls
- Circuit breaker state checks
- Free model quota checks (RPD exhaustion)
- Fallback chain traversal
- Model exclusion list

### B2: Extract ILlmCostService

**Problem**: Cost logging, budget enforcement, and alerting are mixed into the request flow.

**Solution**: Dedicated cost tracking service.

```csharp
public interface ILlmCostService
{
    Task LogRequestAsync(LlmRequestLog log, CancellationToken ct);
    Task<bool> CheckBudgetAsync(string provider, string model, CancellationToken ct);
    Task<LlmCostSummary> GetDailySummaryAsync(DateOnly date, CancellationToken ct);
}
```

### B3: Slim HybridLlmService to Orchestrator

**Problem**: 1100+ LOC, 14 constructor params → hard to test and maintain.

**Target**: ~200-300 LOC, 5 constructor params.

```csharp
public class HybridLlmService : ILlmService
{
    public HybridLlmService(
        ILlmProviderSelector selector,
        IEnumerable<ILlmClient> clients,
        ILlmCostService costService,
        ILogger<HybridLlmService> logger,
        IOptions<AiProviderSettings> settings) { }

    // Orchestration: select → execute → log
}
```

### B4: Unified Configuration Dashboard

**Problem**: LLM config scattered across appsettings.json, secrets, DB, Redis — no single view.

**Solution**: New admin page `/admin/agents/config` showing all 4 layers.

| Layer | Content | Editable? |
|-------|---------|-----------|
| Database | Strategy mappings, tier access | ✅ Yes |
| appsettings.json | Circuit breaker, budgets, fallback chain | ❌ Read-only ("requires redeploy") |
| Redis | RPD exhaustion, RPM counters, circuit breaker state | 🔄 Flushable |
| Secrets | API key status, last rotation date | ❌ Read-only |

### B5: Move Budget/CircuitBreaker Config to Database

**Problem**: Changing circuit breaker thresholds or budget limits requires redeployment.

**Solution**: Database-backed config with `appsettings.json` as defaults.

```csharp
public class LlmSystemConfig : AggregateRoot<Guid>
{
    public int CircuitBreakerFailureThreshold { get; private set; }
    public int CircuitBreakerOpenDurationSeconds { get; private set; }
    public decimal DailyBudgetUsd { get; private set; }
    public decimal MonthlyBudgetUsd { get; private set; }
    public string FallbackChainJson { get; private set; }  // JSONB
    // ... admin-editable at runtime
}
```

---

## Epic C: LLM Transparency & Editor Experience

**Goal**: Users see quality-appropriate badges, editors/admins see full technical details
**Priority**: 🟡 Important
**Estimated Issues**: 4

### Design Decision: 3-Level Transparency

| Level | Shows | Audience |
|-------|-------|----------|
| **Zero** | Nothing — "MeepleAI Assistant" | N/A (rejected) |
| **Soft** | Quality badge: ⚡Fast / 🎯Balanced / 💎Premium | All users |
| **Full** | Provider, model, tokens, cost, latency, sources | Editor/Admin |

**Decision**: Soft for all users + Full for editor/admin.

### C1: ResponseMetaBadge Component

Soft quality badge shown on every AI response.

```typescript
const strategyBadge = {
  'Fast':       { icon: '⚡', label: 'Risposta rapida',      color: 'amber' },
  'Balanced':   { icon: '🎯', label: 'Risposta bilanciata',  color: 'blue' },
  'Premium':    { icon: '💎', label: 'Risposta premium',     color: 'violet' },
  'HybridRAG':  { icon: '🧠', label: 'Analisi approfondita', color: 'emerald' },
};
```

**Acceptance Criteria**:
- Badge appears on every AI chat response
- Tooltip shows brief description of the strategy tier
- No technical details visible to non-editor users
- Mobile responsive

### C2: Technical Details Panel (Editor/Admin)

Expandable panel below each AI response showing full metadata.

**Fields**: Provider, Model, Tokens (prompt + completion), Latency, Cost, Strategy, Sources (chunks + scores), Deep link to Debug Console.

**Acceptance Criteria**:
- Only visible to users with Editor or Admin role
- Collapsible by default (click to expand)
- "View in Debug Console" link opens Debug Console filtered to this request
- Copy-to-clipboard for debugging

### C3: Editor Self-Service Usage Page

**Problem**: Editors use LLM daily but have zero visibility into their usage/cost impact.

**Solution**: New page at `/dashboard/my-ai-usage` (not admin-only).

**Content**:
- Personal request count (today, 7d, 30d)
- Tokens used (prompt + completion)
- Cost impact estimate
- Model distribution pie chart
- Strategy distribution
- Recent requests table (own only)

### C4: Deep Link from Chat to Debug Console

When an editor/admin sees a problematic response, they can click "View details" → opens Debug Console pre-filtered to that specific execution ID.

**Requires**: Response metadata includes `executionId` for editor/admin users.

---

## Epic D: A/B Testing Playground

**Goal**: Editors/admins can blind-compare model quality with structured evaluation
**Priority**: 🟡 Important
**Estimated Issues**: 7

### Domain Model

```csharp
public sealed class AbTestSession : AggregateRoot<Guid>
{
    public Guid CreatedBy { get; private set; }
    public string Query { get; private set; }           // Max 2000 chars
    public Guid? KnowledgeBaseId { get; private set; }
    public AbTestStatus Status { get; private set; }     // Draft, InProgress, Evaluated
    public List<AbTestVariant> Variants { get; }          // 2-4 variants
    public DateTime CreatedAt { get; private set; }
}

public sealed class AbTestVariant : Entity<Guid>
{
    public string Label { get; private set; }             // A, B, C, D
    public string Provider { get; private set; }
    public string ModelId { get; private set; }
    public string Response { get; private set; }
    public int TokensUsed { get; private set; }
    public int LatencyMs { get; private set; }
    public decimal CostUsd { get; private set; }
    public AbTestEvaluation? Evaluation { get; private set; }
}

public sealed class AbTestEvaluation : ValueObject
{
    public Guid EvaluatorId { get; private set; }
    public int Accuracy { get; private set; }       // 1-5
    public int Completeness { get; private set; }   // 1-5
    public int Clarity { get; private set; }        // 1-5
    public int Tone { get; private set; }           // 1-5
    public string? Notes { get; private set; }
    public DateTime EvaluatedAt { get; private set; }
}

public enum AbTestStatus { Draft, InProgress, Evaluated }
```

### D1: AbTestSession Domain Entity + Migration

Domain entity, EF configuration, migration.

### D2: A/B Test CQRS Commands & Queries

- `CreateAbTestCommand` → Generates N variants in parallel, stores blind
- `EvaluateAbTestCommand` → Saves scores for all variants
- `RevealAbTestQuery` → Returns models (only after evaluation)
- `GetAbTestsQuery` → Paginated list with filters
- `GetAbTestAnalyticsQuery` → Aggregated win rates, Elo, scores

### D3: A/B Test Backend Endpoints

```
POST   /api/v1/admin/ab-tests                    → Create + generate
GET    /api/v1/admin/ab-tests                    → List (paginated)
GET    /api/v1/admin/ab-tests/{id}               → Detail (blind)
POST   /api/v1/admin/ab-tests/{id}/evaluate      → Submit scores
GET    /api/v1/admin/ab-tests/{id}/reveal         → Show models
GET    /api/v1/admin/ab-tests/analytics           → Aggregated stats
```

### D4: Frontend — New A/B Test Page

`/admin/agents/ab-testing/new`

- Query input with KB selector
- Model selection checkboxes (2-4 models)
- "Generate" button → shows loading state → redirects to evaluation

### D5: Frontend — Blind Evaluation Page

`/admin/agents/ab-testing/[id]`

- Side-by-side response columns (A, B, C, D)
- Per-variant scoring: Accuracy, Completeness, Clarity, Tone (1-5 stars)
- Optional notes textarea
- "Submit Evaluation" → reveal models

### D6: Frontend — Analytics Dashboard

`/admin/agents/ab-testing/results`

- **Win rate** per model (% times highest score)
- **Elo rating** (pairwise comparison)
- **Score breakdown** per dimension (accuracy, completeness, clarity, tone)
- **Cost-per-quality-point** (cost / avg score)
- **Segmentation** by KB type (rules, FAQ, mechanics)
- Date range filter

### D7: RequestSource.ABTesting + Budget Isolation

- New `RequestSource.ABTesting` enum value
- Separate daily budget for A/B testing (configurable, default $5/day)
- Rate limit: max 50 tests/day per editor, 200 per admin
- 24h response caching for same query+model

---

## Epic E: Model Versioning & Availability Monitoring

**Goal**: Detect deprecated/unavailable models before they impact users
**Priority**: 🟡 Important
**Estimated Issues**: 5

### E1: ModelAvailabilityCheckJob

Quartz job running every 6 hours.

**Logic**:
1. Read all strategy-model mappings from DB
2. For each OpenRouter model: `GET /api/v1/models` → verify exists
3. If model absent or marked deprecated:
   - Publish `ModelDeprecatedEvent`
   - Create admin notification with suggested replacement
   - If mapping has fallbackModels → activate fallback automatically

### E2: Model Compatibility Matrix (DB Entity)

```csharp
public class ModelCompatibilityEntry : Entity<Guid>
{
    public string ModelId { get; private set; }
    public List<string> Alternatives { get; private set; }  // JSONB
    public int ContextWindow { get; private set; }
    public List<string> Strengths { get; private set; }     // JSONB
    public bool IsCurrentlyAvailable { get; private set; }
    public DateTime LastVerified { get; private set; }
}
```

### E3: Admin Notification on Model Deprecation

Admin receives actionable notification:
```
⚠️ Model 'llama-3.3-70b:free' no longer available on OpenRouter
Strategy affected: 'Balanced' (tier Free, User)
Suggested replacement: 'qwen/qwen-2.5-72b:free' (similar, free)
[Apply Replacement] [Ignore] [View Details]
```

### E4: Auto-Fallback on Deprecated Model

If a strategy mapping points to a deprecated model AND the mapping has `fallbackModels`, automatically switch to the first available fallback. Log the switch, notify admin.

### E5: Admin UI — Model Health Page

Add health indicators to existing `/admin/agents/models` page:
- Last verified timestamp per model
- Availability status badge (✅ Available, ⚠️ Deprecated, ❌ Unavailable)
- "Check Now" button to trigger immediate verification
- History of model changes (when swapped, by whom/auto)

---

## Epic F: GDPR Compliance for LLM Subsystem

**Goal**: Ensure legal compliance for AI data processing
**Priority**: 🔴 Critical (legal requirement)
**Estimated Issues**: 11

### Regulatory Context

When MeepleAI sends prompts to OpenRouter (USA-based), it transfers potentially personal data to an external processor. Under GDPR:
- Art. 6: Need legal basis for processing
- Art. 13-14: Transparency obligations
- Art. 17: Right to erasure
- Art. 25: Privacy by design
- Art. 30: Records of processing
- Art. 35: DPIA may be required

### F1: DPA with OpenRouter (Legal)

**Action**: Verify OpenRouter provides a Data Processing Agreement. If not, evaluate alternatives or implement additional safeguards.

**Deliverable**: Documented DPA status + risk assessment.

### F2: Transfer Impact Assessment (Legal)

**Action**: Conduct Transfer Impact Assessment for EU→USA data transfer via OpenRouter.

**Deliverable**: `docs/compliance/transfer-impact-assessment.md`

### F3: DeleteUserLlmDataCommand (Right to Erasure)

```csharp
public record DeleteUserLlmDataCommand(Guid UserId) : IRequest<Unit>;

// Handler deletes:
// 1. LlmRequestLogEntity WHERE UserId = X
// 2. Conversation history WHERE UserId = X
// 3. Redis keys: openrouter:user:{userId}:*
// 4. AbTestEvaluation WHERE EvaluatorId = X
// 5. JSONL log redaction (or accept 30-day auto-cleanup)
```

### F4: PII Detection/Stripping before OpenRouter

**Problem**: User prompts may contain names, emails, phone numbers. These get sent to OpenRouter.

**Solution**: PII detector that strips/masks PII before sending to external providers.

```csharp
public interface IPiiDetector
{
    PiiScanResult Scan(string text);
    string Redact(string text, PiiScanResult result);
}
```

**Scope**: Only for OpenRouter calls. Ollama (local) doesn't need PII stripping.

**Detection targets**: Email regex, phone regex, Italian fiscal code (codice fiscale), names (NER-based or pattern matching).

### F5: Log Pseudonymization after 7 Days

After 7 days, replace `UserId` in `LlmRequestLogEntity` with SHA-256 hash. Preserves analytics capability while reducing PII exposure.

### F6: AI Consent Tracking

Track user consent for AI processing.

```csharp
public class UserAiConsent : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public bool ConsentedToAiProcessing { get; private set; }
    public bool ConsentedToExternalProviders { get; private set; }
    public DateTime ConsentedAt { get; private set; }
    public string ConsentVersion { get; private set; }  // "1.0"
}
```

### F7: AI Opt-Out Mechanism

Users can disable AI features and use traditional search only.

**UI**: Toggle in user settings: "Use AI-powered answers" (default: on).
**Backend**: If opted out, RAG pipeline returns only document search results (no LLM generation).

### F8: Privacy Policy Update (AI Section)

Add AI-specific section to privacy policy:
- What data is sent to AI providers
- Which providers are used (OpenRouter, Ollama)
- Data retention periods
- How to opt out
- How to request data deletion

**Deliverable**: Legal text for privacy policy page.

### F9: Record of Processing Document

Permanent record of LLM processing activities (Art. 30).

**Deliverable**: `docs/compliance/record-of-processing-llm.md`

```yaml
processing_activity: "AI-powered question answering"
data_categories: ["user queries", "chat history", "game rules"]
legal_basis: "Legitimate interest (Art. 6(1)(f)) + consent for external processing"
processors: ["OpenRouter (USA)", "Ollama (self-hosted EU)"]
retention: "30 days individual logs, 90 days chat history, 1 year aggregates"
safeguards: "PII stripping, pseudonymization, local-first routing (80% Ollama)"
```

### F10: OpenRouterFileLogger JSONL Erasure

Include JSONL log files in user data deletion flow.

**Options**:
- A: Grep + redact user-specific entries (complex)
- B: Accept 30-day auto-cleanup as sufficient (pragmatic)
- C: Switch to structured DB logging instead of file-based (cleanest)

**Recommendation**: Option C (migrate to DB) for new entries, Option B for existing.

### F11: DPIA Simplified Assessment

Conduct a simplified Data Protection Impact Assessment.

**Risk assessment**:
- Data sensitivity: Low (board game preferences, not health/financial)
- Profiling: Minimal (tier-based, not behavioral)
- External processing: Medium risk (OpenRouter USA)
- Mitigation: Strong (80% local, PII stripping, pseudonymization)

**Deliverable**: `docs/compliance/dpia-llm.md`

---

## Epic G: Multi-Region Preparation

**Goal**: Prepare architecture for future geographic expansion without over-engineering
**Priority**: 🟢 Low (future preparation)
**Estimated Issues**: 3

### Scaling Roadmap

```
Current      → Docker Compose single-node (Italy)
Phase 1 (<5K users)  → + Cloudflare CDN for frontend
Phase 2 (<20K users) → Kubernetes single-cluster EU + managed Postgres/Redis
Phase 3 (<100K users)→ Multi-region K8s + Qdrant Cloud + OpenRouter primary
Phase 4 (100K+)      → Edge computing + distributed Qdrant + regional Ollama
```

### G1: Add UserRegion to LlmRoutingDecision

```csharp
public class LlmRoutingDecision
{
    public string ProviderName { get; init; }
    public string ModelId { get; init; }
    public string Reasoning { get; init; }
    public string? UserRegion { get; init; }  // NEW: "eu-west", "us-east", etc.
}
```

No behavioral change — field is populated but ignored by routing strategy.

### G2: Region-Aware Routing Strategy (No-Op)

Add region parameter to routing interface. Current implementation ignores it. Future implementation can use it for geographic routing.

### G3: Multi-Region Architecture Document

**Deliverable**: `docs/architecture/adr/adr-XXX-multi-region-strategy.md`

Covers: Ollama placement, Qdrant replication, embedding service location, CDN strategy, cost projections per phase.

---

## Implementation Priority Matrix

| Epic | Priority | Effort | Risk if Delayed | Recommended Phase |
|------|----------|--------|-----------------|-------------------|
| **F: GDPR** | 🔴 Critical | High | Legal liability | Phase 1 (immediate) |
| **A: Operational Maturity** | 🔴 Critical | Medium | Incident response gap | Phase 1 |
| **C: Transparency** | 🟡 Important | Medium | UX/trust debt | Phase 2 |
| **B: Refactoring** | 🟡 Important | High | Tech debt acceleration | Phase 2 |
| **E: Model Versioning** | 🟡 Important | Medium | Silent failures | Phase 2 |
| **D: A/B Testing** | 🟡 Important | High | Quality measurement gap | Phase 3 |
| **G: Multi-Region** | 🟢 Low | Low | None (preparation) | Phase 3 |

### Phase 1 (Immediate — 2-4 weeks)
- F1, F2, F3, F6, F8, F9 (GDPR critical path)
- A1, A2 (emergency controls + Redis detection)
- A3 (NFR documentation)

### Phase 2 (Short-term — 4-8 weeks)
- F4, F5, F7, F10, F11 (GDPR completion)
- C1, C2, C3, C4 (transparency)
- B1, B2, B3 (service decomposition)
- E1, E2, E3, E4, E5 (model versioning)
- A4, A5 (Prometheus + chaos tests)

### Phase 3 (Medium-term — 8-12 weeks)
- B4, B5 (config unification)
- D1-D7 (A/B testing)
- G1, G2, G3 (multi-region prep)

---

## Dependencies

```
A1 (Emergency Controls) ← B5 (Config to DB) — emergency controls use same DB-backed config
B1 (Provider Selector) ← B3 (Slim Service) — extract first, then slim
C2 (Tech Details Panel) ← C1 (Badge) — badge is prerequisite
C4 (Deep Link) ← C2 (Tech Panel) — needs execution ID in response
D1 (AB Domain) ← D2 (CQRS) ← D3 (Endpoints) ← D4-D6 (Frontend)
E1 (Check Job) ← E3 (Notifications) — job publishes events
F3 (Erasure Command) ← F10 (JSONL Erasure) — erasure must include all stores
F4 (PII Detection) → independent, can be done anytime
G1 (Region Field) → G2 (Strategy) — field first, behavior second
```

## Success Metrics

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| Admin incident response time | ~30min (redeploy) | <2min (UI action) | Time from alert to remediation |
| Configuration layers requiring redeploy | 2 of 4 | 0 of 4 | Count of non-runtime-editable configs |
| GDPR compliance items addressed | 0/13 | 13/13 | Checklist completion |
| HybridLlmService constructor params | 14 | 5 | Code metric |
| HybridLlmService LOC | 1100+ | ~250 | Code metric |
| Chaos test coverage for failure paths | 0 | 6 scenarios | Test count |
| Model deprecation detection time | ∞ (manual) | <6h (automated) | Job frequency |
| A/B test evaluations per month | 0 | 100+ | Analytics count |

---

## Appendix: Expert Panel Attribution

| Expert | Domain | Key Contributions |
|--------|--------|-------------------|
| Michael Nygard | Production resilience | Circuit breaker gaps, Redis SPOF, chaos testing |
| Martin Fowler | Architecture | HybridLlmService decomposition, config fragmentation |
| Sam Newman | Distributed systems | Service boundary analysis, operational levers |
| Karl Wiegers | Requirements | Missing NFRs, editor role gap, acceptance criteria |
| Lisa Crispin | Testing | Testability analysis, integration test gaps |
| Kelsey Hightower | Cloud native | Prometheus export, secret management, multi-region |
| Seth Godin | Remarkability | Transparency as differentiator |
| Gojko Adzic | Specification by example | A/B testing scenarios, concrete examples |
