# Epic #3490 - Sequential Implementation Plan with /implementa

> **Execution Mode**: Sequential workflow using `/implementa` for each issue
> **Total**: 9 issues, 60 SP, ~9 sprints
> **Start Date**: 2026-02-15 | **Target Completion**: ~March 2026

---

## 📋 Pre-Flight Checklist

Before starting, verify:
- [ ] On `main-dev` branch with clean workspace
- [ ] All dependencies installed (`dotnet restore`, `pnpm install`)
- [ ] Infra running (`docker compose up -d postgres qdrant redis`)
- [ ] API running on `:8080`, Web on `:3000`
- [ ] All current tests passing (`dotnet test`, `pnpm test`)

---

## 🚀 Sprint 1: Multi-Model Evaluation (5 SP)

### Issue: #4332 - Complete Multi-Model Evaluation

**Goal**: Upgrade `MultiModelEvaluator.cs` from MVP to actual GPT-4 + Claude + DeepSeek consensus

**Current State**:
- ✅ Interface exists: `IMultiModelEvaluator.cs`
- ⚠️ Implementation is MVP: calls same LLM 3x (line 38-43 in `MultiModelEvaluator.cs`)
- ❌ No real multi-model API integration

**What to Build**:
1. GPT-4 evaluator via OpenRouter
2. Claude evaluator via OpenRouter
3. DeepSeek evaluator via OpenRouter
4. Weighted voting consensus algorithm
5. Confidence scoring (model agreement)
6. Fallback logic (model failures)
7. Redis position caching (cost optimization)
8. Unit tests (consensus accuracy >90%)
9. Performance tests (parallel calls <10s P95)

**DoD**:
- [ ] Multi-model consensus improves accuracy >10%
- [ ] Model agreement tracked in confidence scores
- [ ] Fallback works when primary model unavailable
- [ ] Cost optimized via position caching
- [ ] Performance: <10s P95 for dual-model evaluation
- [ ] Tests passing with >90% coverage

**Command**:
```bash
/implementa 4332 --base-branch main-dev --pr-target main-dev
```

**Expected Output**:
- PR to `main-dev` with multi-model implementation
- Issue #4332 closed
- Ready for #4334

---

## 🚀 Sprint 2: Decisore REST API (3 SP)

### Issue: #4334 - Decisore REST API Endpoint Enhancement

**Goal**: Complete `/api/v1/agents/decisore/analyze` with SSE + rate limiting

**Current State**:
- ✅ Basic endpoint exists: `DecisoreAgentEndpoints.cs`
- ❌ Missing SSE streaming for progress
- ❌ Missing rate limiting

**What to Build**:
1. SSE streaming for analysis progress (move evaluation, scoring)
2. Rate limiting (10 expert analyses/min)
3. Enhanced request validation
4. OpenAPI/Scalar documentation
5. API integration tests
6. Performance tests (>20 req/s throughput)
7. Error handling for invalid game states

**DoD**:
- [ ] Endpoint accepts game state + analysis parameters
- [ ] Returns StrategicAnalysisResultDto with move suggestions
- [ ] SSE streams analysis progress (move evaluation, scoring)
- [ ] Rate limiting prevents abuse (10 expert analyses per minute)
- [ ] API documented in Scalar UI
- [ ] Tests passing with >90% coverage

**Dependencies**:
- ✅ #4332 must be merged first

**Command**:
```bash
# AFTER #4332 is merged
/implementa 4334 --base-branch main-dev --pr-target main-dev
```

**Expected Output**:
- PR to `main-dev` with complete Decisore API
- Issue #4334 closed
- Decisore agent fully operational

---

## 🚀 Sprint 3: Arbitro Beta Testing (5 SP)

### Issue: #4328 - Arbitro Beta Testing Infrastructure

**Goal**: Build infrastructure for beta testing Arbitro agent

**Current State**:
- ✅ Arbitro service complete: `ArbitroAgentService.cs`
- ✅ API endpoint complete: `ArbitroAgentEndpoints.cs`
- ❌ No beta testing infrastructure

**What to Build**:
1. Beta testing environment (feature flags, A/B testing config)
2. Feedback collection API endpoint + storage schema
3. Detailed logging for accuracy analysis (structured telemetry)
4. Performance monitoring dashboard metrics
5. Common failure pattern analysis framework
6. FAQ database expansion with test scenarios
7. Performance tuning framework
8. Accuracy validation test suite (>90% target)

**DoD**:
- [ ] Beta testing environment with feature flags
- [ ] Feedback collection mechanism implemented
- [ ] Detailed logging for accuracy analysis
- [ ] Performance metrics tracked
- [ ] Common failure patterns analyzed
- [ ] FAQ database expanded
- [ ] Performance tuned
- [ ] Accuracy >90% validation

**Note**: This builds the *infrastructure*. Actual user beta testing is manual post-implementation.

**Command**:
```bash
# Can run in parallel with #4334, but sequential is safer
/implementa 4328 --base-branch main-dev --pr-target main-dev
```

**Expected Output**:
- PR to `main-dev` with beta testing infrastructure
- Issue #4328 closed
- Arbitro Phase 3 complete (100%)

---

## 🚀 Sprint 4: Decisore Beta Testing (5 SP)

### Issue: #4335 - Decisore Beta Testing Infrastructure

**Goal**: Build infrastructure for beta testing Decisore agent

**What to Build**:
1. Beta testing environment with feature flags
2. Feedback collection API + storage
3. Move quality logging framework
4. Performance monitoring metrics
5. Win/loss correlation tracking
6. Heuristic tuning framework
7. Performance optimization tests (<5s P95)
8. Quality validation (correlation >80%)

**DoD**:
- [ ] Beta testing environment set up
- [ ] Feedback collection implemented
- [ ] Detailed logging for move quality
- [ ] Performance metrics tracked
- [ ] Win/loss correlation tracked
- [ ] Heuristic tuning framework ready
- [ ] Performance <5s P95 for expert
- [ ] Quality correlation >80%

**Dependencies**:
- ✅ #4334 must be merged first (Decisore API complete)

**Command**:
```bash
# AFTER #4334 is merged
/implementa 4335 --base-branch main-dev --pr-target main-dev
```

**Expected Output**:
- PR to `main-dev` with beta testing infrastructure
- Issue #4335 closed
- Decisore Phase 4 complete (100%)

---

## 🚀 Sprint 5: Multi-Agent Router (8 SP) - CRITICAL

### Issue: #4336 - Multi-Agent Router - Intelligent Request Routing

**Goal**: Build intelligent routing to direct queries to correct agent (Tutor/Arbitro/Decisore)

**Current State**:
- ✅ All agent APIs complete
- ❌ No unified routing system

**What to Build**:
1. Multi-agent routing architecture design
2. `AgentRouterService` with intent-based routing
3. Intent classifier for agent selection
4. Confidence thresholds for routing (human review <70%)
5. Fallback logic for ambiguous intents
6. Routing metrics and monitoring
7. Unit tests (routing accuracy >95%)
8. Integration tests with all 3 agents
9. Performance tests (routing <50ms P95)

**DoD**:
- [ ] Routing accuracy >95% for clear intent queries
- [ ] Fallback handling for ambiguous cases
- [ ] Performance: Routing decision <50ms P95
- [ ] All three agents accessible via router
- [ ] Tests passing with >90% coverage

**Dependencies**:
- ✅ All agent APIs: #3499 (Tutor), #4327 (Arbitro), #4334 (Decisore)

**Command**:
```bash
# AFTER #4334 is merged
/implementa 4336 --base-branch main-dev --pr-target main-dev
```

**Expected Output**:
- PR to `main-dev` with multi-agent router
- Issue #4336 closed
- **Unblocks Phase 5 Integration**

---

## 🚀 Sprint 6: Agent State Coordination (8 SP) - CRITICAL

### Issue: #4337 - Agent State Coordination - Shared Context Management

**Goal**: Enable agents to share context for seamless multi-agent workflows

**What to Build**:
1. Shared agent state schema (PostgreSQL + Redis)
2. `AgentStateCoordinator` service
3. `SharedAgentContext` (conversation + game state)
4. State synchronization (event-driven)
5. Context handoff protocol
6. State versioning (optimistic concurrency)
7. Unit tests (consistency validation)
8. Integration tests (multi-agent workflows)
9. Performance tests (sync <100ms P95)

**DoD**:
- [ ] Agents can access shared conversation history
- [ ] Game state synchronized across agent calls
- [ ] Context handoffs preserve full state
- [ ] Performance: State sync <100ms P95
- [ ] No state inconsistencies in concurrent requests
- [ ] Tests passing with >90% coverage

**Dependencies**:
- ✅ #4336 must be merged first (Multi-Agent Router)

**Command**:
```bash
# AFTER #4336 is merged
/implementa 4337 --base-branch main-dev --pr-target main-dev
```

**Expected Output**:
- PR to `main-dev` with state coordination
- Issue #4337 closed
- **Unblocks #4338 and #4339**

---

## 🚀 Sprint 7: Unified API Gateway (5 SP)

### Issue: #4338 - Unified API Gateway - /api/v1/agents/query

**Goal**: Single endpoint that auto-routes to any agent

**What to Build**:
1. `POST /api/v1/agents/query` unified endpoint
2. Automatic agent routing based on intent
3. SSE streaming for multi-agent responses
4. Unified response schema (`AgentResponseDto`)
5. Request validation + JWT auth
6. Rate limiting (per-agent quotas)
7. OpenAPI/Scalar documentation
8. API integration + performance tests

**DoD**:
- [ ] Single endpoint routes to all three agents
- [ ] Automatic agent selection based on intent
- [ ] Unified response format across agents
- [ ] SSE streaming for real-time updates
- [ ] Rate limiting enforces per-agent quotas
- [ ] API fully documented in Scalar UI
- [ ] Tests passing with >90% coverage

**Dependencies**:
- ✅ #4336 (Multi-Agent Router)
- ✅ #4337 (Agent State Coordination)

**Command**:
```bash
# AFTER #4337 is merged
/implementa 4338 --base-branch main-dev --pr-target main-dev
```

**Expected Output**:
- PR to `main-dev` with unified API gateway
- Issue #4338 closed

---

## 🚀 Sprint 8: Performance Optimization (8 SP)

### Issue: #4339 - Performance Optimization & Cross-Agent Caching

**Goal**: Optimize system performance through caching, batching, pooling

**What to Build**:
1. Cross-agent caching strategy design
2. Shared Redis cache (L1/L2/L3 tiers)
3. Request batching for parallel agent calls
4. Resource pooling for LLM connections
5. Cache invalidation strategies
6. Performance monitoring + profiling
7. Context assembly optimization
8. Unit tests (cache consistency)
9. Performance tests (>30% latency reduction, >70% cache hit)

**DoD**:
- [ ] Cache hit rate >70% for frequent queries
- [ ] Latency reduction >30% vs no caching
- [ ] Resource pooling reduces overhead >50%
- [ ] Performance targets met for all agents
- [ ] No cache inconsistencies
- [ ] Tests passing with >90% coverage

**Dependencies**:
- ✅ #4337 (Agent State Coordination)

**Command**:
```bash
# AFTER #4337 is merged (can run parallel with #4338 theoretically)
/implementa 4339 --base-branch main-dev --pr-target main-dev
```

**Expected Output**:
- PR to `main-dev` with performance optimizations
- Issue #4339 closed

---

## 🚀 Sprint 9: Production Deployment (13 SP) - FINAL

### Issue: #4340 - Production Deployment & Monitoring Infrastructure

**Goal**: Deploy multi-agent system with monitoring, alerting, tracing

**What to Build**:
1. Production environment (Docker + Kubernetes)
2. Health checks for all agent services
3. Prometheus metrics (latency, throughput, errors)
4. Grafana dashboards
5. Alerting (PagerDuty/Slack) for SLA violations
6. Distributed tracing (OpenTelemetry)
7. CI/CD automation (GitHub Actions)
8. Log aggregation (ELK stack)
9. Incident response runbooks
10. Load testing (10K concurrent users)

**DoD**:
- [ ] All agents deployed and healthy in production
- [ ] Monitoring dashboards track all key metrics
- [ ] Alerts configured for SLA violations
- [ ] Distributed tracing shows end-to-end flows
- [ ] Load testing validates 10K user capacity
- [ ] Runbooks cover common incidents
- [ ] Zero-downtime deployment validated

**Dependencies**:
- ✅ ALL integration issues (#4336, #4337, #4338, #4339)

**Command**:
```bash
# AFTER #4338 and #4339 are merged
/implementa 4340 --base-branch main-dev --pr-target main-dev
```

**Expected Output**:
- PR to `main-dev` with production deployment
- Issue #4340 closed
- **Epic #3490 100% COMPLETE**

---

## 📊 Quick Copy-Paste Execution Sequence

```bash
# Phase 3-4 Completion (Arbitro Beta + Decisore Completion)
/implementa 4332 --base-branch main-dev --pr-target main-dev  # Multi-Model Eval (5 SP)
/implementa 4334 --base-branch main-dev --pr-target main-dev  # Decisore REST API (3 SP)
/implementa 4328 --base-branch main-dev --pr-target main-dev  # Arbitro Beta (5 SP)
/implementa 4335 --base-branch main-dev --pr-target main-dev  # Decisore Beta (5 SP)

# Phase 5: Integration (Critical Path)
/implementa 4336 --base-branch main-dev --pr-target main-dev  # Multi-Agent Router (8 SP) - CRITICAL
/implementa 4337 --base-branch main-dev --pr-target main-dev  # Agent State Coord (8 SP) - CRITICAL
/implementa 4338 --base-branch main-dev --pr-target main-dev  # Unified API Gateway (5 SP)
/implementa 4339 --base-branch main-dev --pr-target main-dev  # Perf Optimization (8 SP)
/implementa 4340 --base-branch main-dev --pr-target main-dev  # Production Deploy (13 SP)
```

---

## 🎯 Progress Tracking

| Sprint | Issue | Status | PR | Merged |
|:------:|:-----:|:------:|:--:|:------:|
| 1 | #4332 | ⏳ | - | - |
| 2 | #4334 | ⏳ | - | - |
| 3 | #4328 | ⏳ | - | - |
| 4 | #4335 | ⏳ | - | - |
| 5 | #4336 | ⏳ | - | - |
| 6 | #4337 | ⏳ | - | - |
| 7 | #4338 | ⏳ | - | - |
| 8 | #4339 | ⏳ | - | - |
| 9 | #4340 | ⏳ | - | - |

**Update this table after each sprint completion**

---

## 🚨 Critical Notes

1. **Sequential Execution**: Each `/implementa` must complete (PR merged) before starting next
2. **Branch Hygiene**: Always delete feature branch after merge: `git branch -D feature/issue-xxx`
3. **Testing**: Run full test suite before each `/implementa`: `dotnet test && pnpm test`
4. **Code Review**: PRs require review before merge (automated via `/implementa`)
5. **Checkpoints**: Save session context every 30min during `/implementa` execution

---

## 📈 Estimated Timeline

| Phase | Issues | SP | Estimated Duration |
|-------|:------:|:--:|:------------------:|
| Decisore Completion | #4332, #4334 | 8 | 1-1.5 weeks |
| Beta Testing Infra | #4328, #4335 | 10 | 1.5-2 weeks |
| Integration Core | #4336, #4337 | 16 | 2-2.5 weeks |
| Integration Final | #4338, #4339 | 13 | 1.5-2 weeks |
| Production | #4340 | 13 | 2-3 weeks |
| **TOTAL** | **9 issues** | **60 SP** | **8-11 weeks** |

**Target Completion**: Mid-April 2026

---

**Last Updated**: 2026-02-15
**Created By**: PM Agent (SuperClaude)
