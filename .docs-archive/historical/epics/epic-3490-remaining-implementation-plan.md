# Epic #3490 - Multi-Agent Game AI: Remaining Implementation Plan

> **Created**: 2026-02-15 | **Total Remaining**: 60 SP across 9 issues
> **Target**: Complete Phases 3-5 (Arbitro Beta, Decisore Completion, Integration)

## Dependency Graph

```
#4332 Multi-Model Eval (5 SP)          #4328 Arbitro Beta (5 SP)
  ↓                                      [independent]
#4334 Decisore REST API (3 SP)
  ↓
#4335 Decisore Beta (5 SP)
  ↓
#4336 Multi-Agent Router (8 SP)
  ↓
#4337 Agent State Coordination (8 SP)
  ↓                    ↓
#4338 Unified API    #4339 Perf Optimization
  Gateway (5 SP)       & Caching (8 SP)
  ↓                    ↓
  └──── #4340 Production Deploy (13 SP) ────┘
```

## Current Codebase State

| Component | File | Status |
|-----------|------|--------|
| `IMultiModelEvaluator` | `Domain/Services/IMultiModelEvaluator.cs` | ✅ Interface complete |
| `MultiModelEvaluator` | `Domain/Services/MultiModelEvaluator.cs` | ⚠️ MVP (same LLM x3, no real multi-model) |
| `DecisoreAgentEndpoints` | `Routing/DecisoreAgentEndpoints.cs` | ⚠️ Basic endpoint, missing SSE + rate limiting |
| `DecisoreAgentService` | `Domain/Services/DecisoreAgentService.cs` | ✅ Full with ensemble support |
| `ArbitroAgentService` | `Domain/Services/ArbitroAgentService.cs` | ✅ Full with FAQ + conflict resolution |
| `ArbitroAgentEndpoints` | `Routing/ArbitroAgentEndpoints.cs` | ✅ Complete |
| Agent routing files | `Routing/Agent*.cs` | ✅ Per-agent routing exists |

## Execution Sequence (9 Sprints)

### Sprint 1: #4332 - Complete Multi-Model Evaluation (5 SP)

**What**: Upgrade `MultiModelEvaluator.cs` from MVP (same LLM x3) to actual multi-model with GPT-4 + Claude + DeepSeek
**Why**: Foundation for expert-level Decisore analysis. Blocks #4334 completion.

**Key Tasks**:
- Implement actual GPT-4 position evaluator via OpenRouter
- Implement Claude position evaluator via OpenRouter
- Create weighted voting consensus algorithm
- Add confidence scoring based on model agreement
- Implement fallback logic for model failures
- Add Redis position caching for cost optimization
- Unit tests for consensus accuracy validation
- Performance tests for parallel model calls (<10s P95)

**Branch**: `feature/issue-4332-multi-model-evaluation`
**Base**: `main-dev` | **PR Target**: `main-dev`

```bash
/implementa 4332 --base-branch main-dev --pr-target main-dev
```

---

### Sprint 2: #4334 - Decisore REST API Endpoint (3 SP)

**What**: Complete `/api/v1/agents/decisore/analyze` with SSE streaming and rate limiting
**Why**: Exposes Decisore to clients. Blocks beta testing and integration phase.

**Key Tasks**:
- Add SSE streaming for analysis progress (move evaluation, scoring steps)
- Add rate limiting (10 expert analyses/min)
- Enhance request validation (game state, player, max suggestions)
- Complete OpenAPI/Scalar documentation
- Add API integration tests
- Performance tests (throughput >20 req/s for expert analysis)
- Error handling for invalid game states

**Branch**: `feature/issue-4334-decisore-rest-api`
**Base**: `main-dev` | **PR Target**: `main-dev`

**Dependencies**: #4332 ✅ (must be merged first)

```bash
/implementa 4334 --base-branch main-dev --pr-target main-dev
```

---

### Sprint 3: #4328 - Arbitro Beta Testing Infrastructure (5 SP)

**What**: Build beta testing infrastructure for Arbitro Agent
**Why**: Completes Phase 3 (Arbitro 83% → 100%). Validates accuracy in real scenarios.

**Key Tasks**:
- Set up beta testing environment (feature flags, A/B testing)
- Implement feedback collection mechanism (API endpoint + storage)
- Add detailed logging for accuracy analysis (structured telemetry)
- Create performance monitoring dashboard metrics
- Analyze common failure patterns (test scenario suite)
- Expand FAQ database with test conflict scenarios
- Performance tuning based on usage patterns
- Final accuracy validation tests (target >90%)

**Branch**: `feature/issue-4328-arbitro-beta-testing`
**Base**: `main-dev` | **PR Target**: `main-dev`

**Note**: The infrastructure/code part is implementable. Actual user beta testing is manual.

```bash
/implementa 4328 --base-branch main-dev --pr-target main-dev
```

---

### Sprint 4: #4335 - Decisore Beta Testing Infrastructure (5 SP)

**What**: Build beta testing infrastructure for Decisore Agent
**Why**: Validates move suggestion quality. Completes Phase 4.

**Key Tasks**:
- Set up beta testing environment with feature flags
- Implement feedback collection mechanism
- Add detailed logging for move quality analysis
- Create performance monitoring metrics
- Implement win/loss correlation tracking
- Heuristic tuning framework
- Performance optimization tests (target <5s P95 expert)
- Final quality validation (correlation >80%)

**Branch**: `feature/issue-4335-decisore-beta-testing`
**Base**: `main-dev` | **PR Target**: `main-dev`

**Dependencies**: #4334 ✅ (Decisore REST API must be complete)

```bash
/implementa 4335 --base-branch main-dev --pr-target main-dev
```

---

### Sprint 5: #4336 - Multi-Agent Router (8 SP) - CRITICAL

**What**: Intelligent routing system to direct queries to correct agent (Tutor/Arbitro/Decisore)
**Why**: Core integration component. Blocks all remaining Phase 5 issues.

**Key Tasks**:
- Design multi-agent routing architecture
- Create `AgentRouterService` with intent-based routing
- Implement intent classifier for agent selection
- Add confidence thresholds for routing decisions
- Create fallback logic for ambiguous intents (human review <70%)
- Add routing metrics and monitoring
- Unit tests (routing accuracy >95%)
- Integration tests with all three agents
- Performance tests (routing decision <50ms P95)

**Branch**: `feature/issue-4336-multi-agent-router`
**Base**: `main-dev` | **PR Target**: `main-dev`

**Dependencies**: All 3 agent APIs complete (#3499✅, #4327✅, #4334✅)

```bash
/implementa 4336 --base-branch main-dev --pr-target main-dev
```

---

### Sprint 6: #4337 - Agent State Coordination (8 SP) - CRITICAL

**What**: Shared context management for seamless multi-agent workflows
**Why**: Enables context handoffs (e.g., Tutor teaches rule → Arbitro validates using same context)

**Key Tasks**:
- Design shared agent state schema (PostgreSQL + Redis)
- Implement `AgentStateCoordinator` service
- Create `SharedAgentContext` with conversation + game state
- Add state synchronization across agents (event-driven)
- Implement context handoff protocol
- Add state versioning (optimistic concurrency)
- Unit tests (state consistency validation)
- Integration tests (multi-agent workflows)
- Performance tests (state sync <100ms P95)

**Branch**: `feature/issue-4337-agent-state-coordination`
**Base**: `main-dev` | **PR Target**: `main-dev`

**Dependencies**: #4336 ✅ (Multi-Agent Router)

```bash
/implementa 4337 --base-branch main-dev --pr-target main-dev
```

---

### Sprint 7: #4338 - Unified API Gateway (5 SP)

**What**: Single `/api/v1/agents/query` endpoint that auto-routes to any agent
**Why**: Simplifies client integration. Single entry point for all agent interactions.

**Key Tasks**:
- Create `POST /api/v1/agents/query` unified endpoint
- Implement automatic agent routing based on query intent
- Add SSE streaming for multi-agent responses
- Create unified response schema (`AgentResponseDto`)
- Add request validation and JWT authentication
- Add rate limiting (per-agent quotas)
- Create OpenAPI/Scalar documentation
- API integration tests + performance tests

**Branch**: `feature/issue-4338-unified-api-gateway`
**Base**: `main-dev` | **PR Target**: `main-dev`

**Dependencies**: #4336 ✅, #4337 ✅

```bash
/implementa 4338 --base-branch main-dev --pr-target main-dev
```

---

### Sprint 8: #4339 - Performance Optimization & Caching (8 SP)

**What**: Cross-agent caching, request batching, resource pooling
**Why**: Ensures system meets performance targets at scale.

**Key Tasks**:
- Design cross-agent caching strategy
- Implement shared Redis cache layer (L1 hot/L2 warm/L3 cold)
- Add request batching for parallel agent calls
- Create resource pooling for LLM connections
- Implement cache invalidation strategies
- Add performance monitoring and profiling
- Optimize context assembly for multi-agent workflows
- Unit tests (cache consistency)
- Performance tests (>30% latency reduction, >70% cache hit rate)

**Branch**: `feature/issue-4339-performance-optimization`
**Base**: `main-dev` | **PR Target**: `main-dev`

**Dependencies**: #4337 ✅

```bash
/implementa 4339 --base-branch main-dev --pr-target main-dev
```

---

### Sprint 9: #4340 - Production Deployment & Monitoring (13 SP) - FINAL

**What**: Deploy multi-agent system with monitoring, alerting, distributed tracing
**Why**: Production readiness. Final step before public launch.

**Key Tasks**:
- Set up production environment (Docker + Kubernetes)
- Implement health checks for all agent services
- Add Prometheus metrics (latency, throughput, error rates)
- Configure Grafana dashboards
- Set up alerting (PagerDuty/Slack) for SLA violations
- Implement distributed tracing (OpenTelemetry)
- Create deployment automation (CI/CD via GitHub Actions)
- Add log aggregation (ELK stack)
- Document runbooks for incident response
- Load testing (10K concurrent users target)

**Branch**: `feature/issue-4340-production-deployment`
**Base**: `main-dev` | **PR Target**: `main-dev`

**Dependencies**: ALL integration issues (#4336-#4339) ✅

```bash
/implementa 4340 --base-branch main-dev --pr-target main-dev
```

---

## Parallel Optimization Opportunities

While the `/implementa` workflow runs sequentially, these pairs could theoretically run in parallel:

| Pair | Condition |
|------|-----------|
| #4332 + #4328 | Independent (Decisore vs Arbitro) |
| #4338 + #4339 | Both depend on #4337, no inter-dependency |

**Recommended**: Keep sequential for code review quality. Use parallel only under time pressure.

## Summary Table

| Order | Issue | Title | SP | Phase | Dependencies | Cumulative SP |
|:-----:|:-----:|-------|:--:|:-----:|:------------:|:-------------:|
| 1 | #4332 | Multi-Model Evaluation | 5 | Decisore | - | 5 |
| 2 | #4334 | Decisore REST API | 3 | Decisore | #4332 | 8 |
| 3 | #4328 | Arbitro Beta Testing | 5 | Arbitro | - | 13 |
| 4 | #4335 | Decisore Beta Testing | 5 | Decisore | #4334 | 18 |
| 5 | #4336 | Multi-Agent Router | 8 | Integration | #4334 | 26 |
| 6 | #4337 | Agent State Coordination | 8 | Integration | #4336 | 34 |
| 7 | #4338 | Unified API Gateway | 5 | Integration | #4336, #4337 | 39 |
| 8 | #4339 | Perf Optimization | 8 | Integration | #4337 | 47 |
| 9 | #4340 | Production Deployment | 13 | Integration | #4336-#4339 | 60 |

## Quick Execute

Copy-paste these commands in order:

```bash
# Phase 3 Completion + Phase 4 Completion
/implementa 4332
/implementa 4334
/implementa 4328
/implementa 4335

# Phase 5: Integration
/implementa 4336
/implementa 4337
/implementa 4338
/implementa 4339
/implementa 4340
```

## Risk Assessment

| Risk | Probability | Impact | Mitigation |
|------|:-----------:|:------:|------------|
| Multi-model API costs | Medium | Medium | Redis position caching, request batching |
| Intent classification accuracy <95% | Medium | High | Start with keyword rules, iterate to ML |
| State consistency under concurrency | High | High | Optimistic concurrency + Redis locking |
| Production load testing gaps | Low | Critical | Progressive rollout, feature flags |
| Beta testing user recruitment | Medium | Low | Use internal team first, then expand |

---

**Estimated Total Effort**: 60 SP across 9 sprints
**Critical Path**: #4332 → #4334 → #4336 → #4337 → #4338/#4339 → #4340
