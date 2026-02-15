# Implementation Sequence: Playground POC Strategy + RAG Debug Console

**Epics**: #4435 (POC Strategy) + #4436 (RAG Debug)
**Created**: 2026-02-15
**Base Branch**: `main-dev`
**Approach**: Sequential waves, each issue = branch → implement → test → PR → merge → close

---

## Dependency Graph

```
#4437 (POC Strategy Selector) ◄── FOUNDATION
  │
  ├──► #4441 (RAG Strategy Info Panel)     [needs strategy in SSE events]
  ├──► #4438 (LLM Provider Override)       [extends handler with override]
  ├──► #4439 (Real Cost Tracking)          [needs strategy-aware costs]
  │
  ├──► #4442 (Per-Step Pipeline Timing)    [needs instrumented handler]
  │      ├──► #4443 (Cache Observability)  [extends instrumentation]
  │      ├──► #4444 (API Call Tracing)     [extends instrumentation]
  │      └──► #4445 (Developer Console)    [consumes all debug events]
  │
  ├──► #4440 (Strategy Comparison)         [needs all strategies working]
  └──► #4446 (TOMAC-RAG Viz)              [low priority, mostly frontend]
```

---

## Execution Sequence

### Wave 1: Foundation (CRITICAL PATH)

#### Step 1 → Issue #4437 - POC Strategy Selector (Default SingleModel)
- **Branch**: `feature/issue-4437-poc-strategy-selector`
- **Parent**: `main-dev`
- **Priority**: P1 Critical
- **Scope**: Backend + Frontend
- **Estimated effort**: Large

**Backend changes**:
1. Add `AgentSearchStrategy? Strategy` to `PlaygroundChatCommand` (default: SingleModel)
2. Add `int? Strategy` to `PlaygroundChatRequest`
3. Update `AgentPlaygroundEndpoints.cs` to map strategy from request
4. Modify `PlaygroundChatCommandHandler`:
   - Branch on strategy (like `AskAgentQuestionCommandHandler` lines 136-190)
   - `RetrievalOnly`: emit Citations + Complete (no Token events, no LLM call)
   - `SingleModel` (default): current behavior (RAG + stream LLM)
   - `MultiModelConsensus`: dual-model calls with consensus
5. Include strategy name in SSE `Complete` event metadata
6. Ensure `AgentDefinition.Create()` defaults to POC strategy
7. Verify `SeedAgentDefinitionsCommandHandler` seeds with POC default

**Frontend changes**:
1. Add `selectedStrategy` to playground store
2. Add strategy radio group in playground page (3 options)
3. Pre-select "SingleModel (POC)" as default
4. Pass strategy in fetch body to SSE endpoint
5. Show strategy description tooltip per option
6. Display used strategy in debug panel from Complete event

**Tests**:
- Backend: 3 strategy paths (RetrievalOnly returns no tokens, SingleModel streams, MultiModel consensus)
- Frontend: Strategy selector renders, default is SingleModel, strategy sent in request

**DoD**: All 3 strategies work via SSE streaming, POC is default, new agents default to POC

---

### Wave 2: Quick Wins (parallel-safe, touch different parts)

#### Step 2 → Issue #4441 - RAG Strategy Info Panel
- **Branch**: `feature/issue-4441-rag-strategy-info`
- **Parent**: `main-dev`
- **Priority**: P2
- **Scope**: Backend (small) + Frontend (medium)
- **Depends on**: #4437 (strategy info in SSE events)

**Backend changes**:
1. Extend SSE `Complete` event to include strategy details:
   - Strategy name, type, description
   - Parameters: TopK, MinScore, weights (from AgentStrategy value object)
   - Search type (hybrid/vector/keyword)
2. New SSE event or extend `StateUpdate` with `StrategyInfo` sub-type

**Frontend changes**:
1. New "Strategy Info" collapsible section in `DebugPanel.tsx`
2. Display: strategy name badge, parameter table (TopK, MinScore, weights)
3. Search type icon (hybrid/vector/keyword)
4. Update store with strategy config from SSE

**Tests**: Strategy info renders correctly, parameters match agent config

---

#### Step 3 → Issue #4438 - LLM Provider/Model Override
- **Branch**: `feature/issue-4438-llm-provider-override`
- **Parent**: `main-dev`
- **Priority**: P2
- **Scope**: Backend (medium) + Frontend (medium)
- **Depends on**: #4437 (extended handler)

**Backend changes**:
1. Add `string? ModelOverride` and `string? ProviderOverride` to `PlaygroundChatCommand`
2. Add to `PlaygroundChatRequest`
3. In handler: if override provided, use it instead of agent's config
4. Validate model availability (check provider supports the model)
5. Include actual model/provider used in Complete event

**Frontend changes**:
1. Collapsible "Advanced Options" section in playground
2. Model dropdown (list available models per provider)
3. Provider selector (Ollama / OpenRouter)
4. "Reset to Agent Default" button
5. Debug panel shows actual model/provider used vs configured

**Tests**: Override works, fallback to agent default, invalid model rejected

---

### Wave 3: Observability Foundation

#### Step 4 → Issue #4439 - Real Cost Tracking
- **Branch**: `feature/issue-4439-real-cost-tracking`
- **Parent**: `main-dev`
- **Priority**: P2
- **Scope**: Backend (medium) + Frontend (small)
- **Depends on**: #4437 (strategy-aware handler)

**Backend changes**:
1. Port cost calculation from `AskAgentQuestionCommandHandler` to playground handler
2. Track per-component: embedding cost + search cost + LLM cost
3. Persist to `LlmCostLog` table (reuse existing `PersistCostLog()` pattern)
4. Extend SSE Complete event with `CostBreakdown`:
   - `embeddingCost`, `searchCost`, `llmCost`, `totalCost`
   - `provider`, `model`, `tokensPerDollar`
5. RetrievalOnly: emit $0.00 cost

**Frontend changes**:
1. Replace estimate `(tokens/1000 * 0.003)` with real cost from backend
2. Show cost breakdown in debug panel (per-component)
3. Color-code: green (free/cheap), yellow (moderate), red (expensive)
4. Cumulative session cost counter

**Tests**: Cost breakdown matches strategy, RetrievalOnly = $0, costs persisted in DB

---

#### Step 5 → Issue #4442 - Per-Step Pipeline Timing Waterfall
- **Branch**: `feature/issue-4442-pipeline-timing`
- **Parent**: `main-dev`
- **Priority**: P2
- **Scope**: Backend (medium) + Frontend (large)
- **Depends on**: #4437 (instrumented handler)

**Backend changes**:
1. Instrument each pipeline step with `Stopwatch`:
   - Agent loading (ms)
   - Embedding generation (ms)
   - Vector search (ms)
   - Reranking (ms) — if applicable
   - Context building (ms)
   - LLM generation: first-token-time + total (ms)
   - Post-processing (ms)
2. New SSE event type `PipelineStep` or extend `StateUpdate`:
   ```json
   { "step": "vector_search", "startMs": 142, "durationMs": 87, "itemsProcessed": 5 }
   ```
3. Include full pipeline timing array in Complete event

**Frontend changes**:
1. New waterfall visualization component in debug panel
2. Horizontal bars: step name, duration, % of total
3. Color-coded by type: retrieval=blue, compute=orange, LLM=purple
4. Hover tooltip: step details (items processed, input/output size)
5. First-token-time annotation on LLM bar

**Tests**: All steps emit timing, waterfall renders, times sum ≈ total latency

---

### Wave 4: Deep Observability (extends Wave 3 instrumentation)

#### Step 6 → Issue #4443 - Cache Observability
- **Branch**: `feature/issue-4443-cache-observability`
- **Parent**: `main-dev`
- **Priority**: P2
- **Scope**: Backend (medium) + Frontend (small)
- **Depends on**: #4442 (pipeline instrumentation pattern)

**Backend changes**:
1. Instrument HybridCache calls in handler:
   - Cache lookup: hit/miss
   - Cache key (hashed)
   - TTL remaining (if hit), item age
2. Emit cache status in SSE `StateUpdate` or new `CacheEvent` type
3. Include cache summary in Complete event

**Frontend changes**:
1. "Cache" section in Debug Panel
2. Badge: HIT (green) / MISS (red) / SKIP (gray)
3. Details: key, TTL, age, cache type
4. Session hit rate counter

**Tests**: Cache hit/miss reported correctly, session stats accumulate

---

#### Step 7 → Issue #4444 - API Call Tracing
- **Branch**: `feature/issue-4444-api-call-tracing`
- **Parent**: `main-dev`
- **Priority**: P2
- **Scope**: Backend (large) + Frontend (large)
- **Depends on**: #4442 (pipeline instrumentation pattern)

**Backend changes**:
1. Create `ApiTraceCollector` middleware/service
2. Instrument external calls:
   - Embedding API: model, input size, output dimensions, latency
   - Qdrant: collection, query params, results count, latency
   - LLM API: provider, model, prompt size, completion size, status, latency
   - Reranker: input count, output count, latency
3. New SSE event type `ApiTrace`:
   ```json
   { "service": "qdrant", "method": "search", "status": 200, "requestSize": 1024, "responseSize": 4096, "latencyMs": 87 }
   ```
4. Mask sensitive data (API keys, tokens)

**Frontend changes**:
1. New "Network" tab in debug panel (alongside Debug/RAG/Scenarios)
2. Table: timestamp, service, method, status, size, latency
3. Expandable rows: request/response details (payload preview)
4. Filter by service type, sort by latency/timestamp
5. Total calls count + aggregate bandwidth

**Tests**: All external calls traced, no sensitive data leaked, filter/sort works

---

### Wave 5: Consumer Layer

#### Step 8 → Issue #4445 - Developer Console
- **Branch**: `feature/issue-4445-developer-console`
- **Parent**: `main-dev`
- **Priority**: P2
- **Scope**: Frontend (large)
- **Depends on**: #4442, #4443, #4444 (needs rich events to display)

**Backend changes** (minimal):
1. Add log-level metadata to SSE events (info/warn/error/debug)
2. Emit debug-level events in playground mode only

**Frontend changes**:
1. New "Console" tab in debug panel
2. Log entries: timestamp, level, source (RAG/LLM/Cache/API), message
3. Filter by level (checkboxes: info/warn/error/debug)
4. Filter by source (dropdown)
5. Text search across logs
6. Auto-scroll with pause on hover
7. Clear + export (JSON/text) buttons
8. Color-coded by level

**Tests**: Console renders all log types, filters work, export produces valid output

---

### Wave 6: Advanced Features (Low Priority)

#### Step 9 → Issue #4440 - Strategy Comparison Mode
- **Branch**: `feature/issue-4440-strategy-comparison`
- **Parent**: `main-dev`
- **Priority**: P3
- **Scope**: Frontend (large)
- **Depends on**: #4437 (all strategies working), #4439 (cost tracking for comparison)

**Backend changes**: None (reuses playground chat endpoint with different strategy params)

**Frontend changes**:
1. "Compare" toggle/button in playground header
2. Sends same question with all 3 strategies (3 parallel SSE connections)
3. Split view: 3 columns (RetrievalOnly | SingleModel | MultiModelConsensus)
4. Per-column: response, tokens, cost, latency, confidence
5. Summary row: comparison table highlighting best/worst per metric
6. Export comparison results

**Tests**: 3 parallel requests fire, results display in columns, metrics compared

---

#### Step 10 → Issue #4446 - TOMAC-RAG Layer Visualization
- **Branch**: `feature/issue-4446-tomac-rag-viz`
- **Parent**: `main-dev`
- **Priority**: P3
- **Scope**: Backend (small) + Frontend (medium)
- **Depends on**: #4442 (timing data for active layers)

**Backend changes**:
1. Include TOMAC layer activation data in Complete event
2. For L3 (Retrieval) and L5 (Generation): per-layer metrics
3. For L1, L2, L4, L6: status "planned"

**Frontend changes**:
1. New TOMAC visualization component
2. Vertical pipeline: 6 layers with arrows
3. Each layer: status badge (Active green / Planned gray / Bypassed orange)
4. Active layers: expand to show metrics
5. Planned layers: description of future capability

**Tests**: All 6 layers render, active layers show metrics

---

## Implementation Commands

Per ogni issue, il flusso `/implement` è:

```bash
# 1. Create branch
git checkout main-dev && git pull
git checkout -b feature/issue-{N}-{desc}
git config branch.feature/issue-{N}-{desc}.parent main-dev

# 2. Implement (backend → frontend → tests)
# ... code changes ...

# 3. Quality check
cd apps/api/src/Api && dotnet build && dotnet test --filter "Playground"
cd apps/web && pnpm typecheck && pnpm lint && pnpm test

# 4. Commit
git add <files> && git commit -m "feat(playground): description (#issue-N)"

# 5. Push + PR
git push -u origin feature/issue-{N}-{desc}
gh pr create --base main-dev --title "feat(playground): description" --body "..."

# 6. After merge
git checkout main-dev && git pull
git branch -D feature/issue-{N}-{desc}
git remote prune origin

# 7. Close issue
gh issue close {N} --comment "Implemented in PR #..."
```

---

## Summary Table

| Wave | Step | Issue | Title | Depends On | Scope | Priority |
|------|------|-------|-------|-----------|-------|----------|
| 1 | 1 | #4437 | POC Strategy Selector | — | BE+FE Large | P1 |
| 2 | 2 | #4441 | RAG Strategy Info Panel | #4437 | BE+FE Small | P2 |
| 2 | 3 | #4438 | LLM Provider Override | #4437 | BE+FE Medium | P2 |
| 3 | 4 | #4439 | Real Cost Tracking | #4437 | BE+FE Medium | P2 |
| 3 | 5 | #4442 | Per-Step Pipeline Timing | #4437 | BE+FE Large | P2 |
| 4 | 6 | #4443 | Cache Observability | #4442 | BE+FE Medium | P2 |
| 4 | 7 | #4444 | API Call Tracing | #4442 | BE+FE Large | P2 |
| 5 | 8 | #4445 | Developer Console | #4442-44 | FE Large | P2 |
| 6 | 9 | #4440 | Strategy Comparison | #4437,#4439 | FE Large | P3 |
| 6 | 10 | #4446 | TOMAC-RAG Viz | #4442 | BE+FE Medium | P3 |

**Total**: 10 issue, 6 wave, sequenza ottimizzata per dipendenze tecniche e valore di business.
