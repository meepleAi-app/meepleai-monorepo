# RAG Dashboard Real Metrics + CRAG Tracking — Design Spec

**Goal:** Replace 4 mocked RAG dashboard query handlers with real EF Core aggregations over the `RagExecution` table, and add CRAG verdict tracking to enable quality reporting.

**Context:** The RAG system is in staging. `RagExecution` rows are created on every playground chat via `PlaygroundChatCommandHandler`. Dashboard handlers currently return deterministic mock data (seeded random). Feature flags for RAPTOR, GraphRAG, and CRAG already exist and are wired in `RagPromptAssemblyService`.

---

## Scope

### In Scope
- Replace 4 mocked query handlers with real DB queries
- Add `CragVerdict` field to `RagExecution` entity
- Add repository method for metrics aggregation
- EF migration for new column
- Unit tests for all rewritten handlers

### Out of Scope
- Frontend dashboard (Phase 2)
- `GetRagDashboardOptions` handler (stays hardcoded — reference data)
- `GetRagConfig` handler (already real)
- RAPTOR/GraphRAG plugin extraction (already working inline)
- New endpoints or DTOs (existing contracts preserved)

---

## Data Source

**Entity:** `RagExecution` at `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/RagExecution.cs`

**Note:** There are two `RagExecution` entities in the codebase: one in `KnowledgeBase/Domain/Entities/` (metrics tracking — this spec targets it) and one in `Administration/Domain/Aggregates/` (replay feature). Changes here apply ONLY to the KnowledgeBase entity.

**Relevant fields for metrics:**
- `Strategy` (string) — RAG strategy used
- `TotalLatencyMs` (int) — end-to-end latency in milliseconds
- `PromptTokens`, `CompletionTokens`, `TotalTokens` (int) — token usage
- `TotalCost` (decimal) — LLM cost
- `Confidence` (double?) — query-answer confidence score. **Note**: The existing `StrategyMetricsDto` exposes both `AverageRelevanceScore` and `AverageConfidenceScore`. Since `RagExecution` only has `Confidence`, map it to BOTH fields (they are semantically the same metric — confidence of retrieval relevance).
- `CacheHit` (bool) — whether result was cached
- `Status` (string) — "Success" or "Error"
- `ErrorMessage` (string?) — error details
- `CreatedAt` (DateTime) — timestamp for time-series bucketing

**New field:**
- `CragVerdict` (string?) — "Correct", "Incorrect", "Ambiguous", or null (when CRAG not active)

---

## Components

### 1. EF Migration: Add CragVerdict to RagExecution

**File:** `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/RagExecution.cs`

Add nullable string property `CragVerdict` with private setter. Update `Create()` factory method to accept optional `cragVerdict` parameter.

**Migration:** `dotnet ef migrations add AddCragVerdictToRagExecution`

### 2. Repository: IRagExecutionRepository.GetAggregatedMetricsAsync

**File:** `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Repositories/IRagExecutionRepository.cs`

New method:
```csharp
Task<IReadOnlyList<StrategyAggregateMetrics>> GetAggregatedMetricsAsync(
    DateOnly? startDate, DateOnly? endDate,
    string? strategy = null,
    CancellationToken cancellationToken = default);
```

**Returns** (new domain model):
```csharp
public sealed record StrategyAggregateMetrics(
    string Strategy,
    int TotalQueries,
    double AverageLatencyMs,
    double P95LatencyMs,
    double P99LatencyMs,
    double AverageConfidence,       // Maps to BOTH AverageRelevanceScore and AverageConfidenceScore in DTO
    int CacheHits,                  // From RagExecution.CacheHit field (per-execution tracking)
    int CacheMisses,
    long TotalTokensUsed,
    decimal TotalCost,
    decimal AverageCostPerQuery,
    int ErrorCount,
    double ErrorRate,
    int CragCorrect,
    int CragIncorrect,
    int CragAmbiguous,
    DateTime LastUpdated);
```

**Implementation** in `RagExecutionRepository.cs`: Single EF query with `GroupBy(e => e.Strategy)` and aggregate projections. P95/P99 use `OrderBy + Skip/Take` subqueries or raw SQL `PERCENTILE_CONT`.

### 3. Repository: GetTimeSeriesMetricsAsync

New method for time-bucketed data:
```csharp
Task<IReadOnlyList<TimeSeriesPoint>> GetTimeSeriesMetricsAsync(
    string strategy,
    DateOnly startDate, DateOnly endDate,
    TimeSeriesGranularity granularity,
    CancellationToken cancellationToken = default);
```

**Returns:**
```csharp
public sealed record TimeSeriesPoint(
    DateTime Bucket,
    int QueryCount,
    double AverageLatencyMs,
    double AverageConfidence,
    decimal TotalCost);

public enum TimeSeriesGranularity { Hour, Day, Week }
```

**Note**: `TimeSeriesGranularity` is internal to the repository. The existing `GetStrategyTimeSeriesQuery` uses `string Granularity` — the handler parses the string to the enum before calling the repository. No query contract change.
```

**Implementation:** Uses PostgreSQL `date_trunc` via raw SQL or EF `GroupBy` on truncated timestamp.

### 4. Handler Rewrites

All 4 handlers stay in `RagDashboardQueryHandlers.cs`. Same file, same DTOs, same endpoint contracts. Only the implementation changes from mock → real.

#### GetRagDashboardOverviewQueryHandler
- Call `GetAggregatedMetricsAsync(startDate, endDate)` (no strategy filter = all strategies)
- Map `StrategyAggregateMetrics` → `StrategyMetricsDto`:
  - `StrategyId` = `metrics.Strategy` (e.g., "Hybrid")
  - `StrategyName` = `RagStrategy.Parse(metrics.Strategy).GetDisplayName()` (uses existing enum helper, e.g., "HYBRID")
  - `AverageRelevanceScore` = `metrics.AverageConfidence` (same source)
  - `AverageConfidenceScore` = `metrics.AverageConfidence` (same source)
  - `CacheHitRate` = `metrics.CacheHits / (metrics.CacheHits + metrics.CacheMisses)` (per-execution data, NOT from IHybridCacheService)
- Calculate `BestPerformingStrategy` (highest avg confidence) and `MostCostEffectiveStrategy` (lowest cost/query with >10 queries)
- **Note**: Remove `IHybridCacheService` dependency from this handler. Cache stats from `RagExecution.CacheHit` are more accurate per-strategy than global cache service stats.

#### Overlap with existing `GetStatsAsync`
The repository already has `GetStatsAsync()` returning `RagExecutionStats` (global aggregates). Leave it as-is — it serves the quality report endpoint. The new `GetAggregatedMetricsAsync` is per-strategy and date-filtered, a superset. No deprecation needed; they serve different consumers.

#### GetStrategyMetricsQueryHandler
- Call `GetAggregatedMetricsAsync(startDate, endDate, strategy: request.StrategyId)`
- Map single result → `StrategyMetricsDto`

#### GetStrategyTimeSeriesQueryHandler
- Call `GetTimeSeriesMetricsAsync(strategy, startDate, endDate, granularity)`
- Map to `StrategyTimeSeriesMetricsDto` with 4 trend arrays

#### GetStrategyComparisonQueryHandler
- Call `GetAggregatedMetricsAsync(startDate, endDate)` for all strategies (or filtered set)
- Calculate rankings: latency (lower=better), quality (higher=better), cost efficiency (lower=better)
- Generate recommendation using weighted scoring (30% latency, 50% quality, 20% cost)

### 5. CRAG Verdict Population

**Current flow:** `RagPromptAssemblyService` evaluates CRAG and increments `MeepleAiMetrics.RagCragVerdicts` counter, but does NOT return the verdict to the caller. The verdict is fire-and-forget.

**Required plumbing:**
1. `RagPromptAssemblyService.AssembleAsync()` must return the CRAG verdict string as part of its result (add to return DTO or out parameter)
2. The caller (`PlaygroundChatCommandHandler` or `StreamQaCommandHandler`) captures the verdict
3. Pass it to `RagExecution.Create(..., cragVerdict: verdict)`

**If plumbing is complex** (touches too many call sites): defer verdict population to Phase 2. Still add the `CragVerdict` column in the migration (nullable, defaults to null). This unblocks the dashboard schema without blocking on the pipeline change.

### 5b. Composite Index for Dashboard Queries

Add a composite index in the migration to optimize the aggregation queries:

```sql
CREATE INDEX IX_rag_executions_strategy_created_at
ON rag_executions (strategy, created_at DESC);
```

This supports both `GetAggregatedMetricsAsync` (GROUP BY strategy WHERE created_at range) and `GetTimeSeriesMetricsAsync` (date_trunc grouping filtered by strategy).

### 6. Handling Empty Data

When no `RagExecution` rows exist for a date range:
- `GetRagDashboardOverview` returns empty `Strategies` list with zeroed `AggregatedMetrics`
- `GetStrategyMetrics` returns zeroed `StrategyMetricsDto`
- `GetStrategyTimeSeries` returns empty trend arrays
- `GetStrategyComparison` returns empty rankings with no recommendation

No errors, no mock fallback. Empty state is valid.

---

## Testing

### Unit Tests (rewrite existing mocked tests)
- `GetRagDashboardOverviewQueryHandlerTests` — mock `IRagExecutionRepository`, verify aggregation logic
- `GetStrategyMetricsQueryHandlerTests` — single strategy, empty data, error rate calculation
- `GetStrategyTimeSeriesQueryHandlerTests` — hourly/daily/weekly bucketing
- `GetStrategyComparisonQueryHandlerTests` — ranking algorithm, weighted scoring, recommendation

### Integration Tests
- `RagExecutionRepositoryTests` — verify `GetAggregatedMetricsAsync` and `GetTimeSeriesMetricsAsync` against real PostgreSQL with seeded data
- Test P95/P99 percentile calculations
- Test time bucketing accuracy

---

## Files Changed

| Action | File |
|---|---|
| Modify | `KnowledgeBase/Domain/Entities/RagExecution.cs` (add CragVerdict) |
| Modify | `KnowledgeBase/Domain/Repositories/IRagExecutionRepository.cs` (add 2 methods) |
| Modify | `KnowledgeBase/Infrastructure/Persistence/RagExecutionRepository.cs` (implement 2 methods) |
| Modify | `KnowledgeBase/Application/Queries/RagDashboardQueryHandlers.cs` (rewrite 4 handlers) |
| Create | `KnowledgeBase/Domain/Models/StrategyAggregateMetrics.cs` |
| Create | `KnowledgeBase/Domain/Models/TimeSeriesPoint.cs` |
| Create | EF Migration `AddCragVerdictToRagExecution` (column + composite index) |
| Modify | Tests for all 4 handlers + new integration tests |

---

## Risks

- **P95/P99 in EF Core**: PostgreSQL supports `PERCENTILE_CONT` but EF Core doesn't translate it. Use raw SQL via `FromSqlRaw` or approximate with `OrderBy + Skip`.
- **Empty RagExecution table in staging**: Dashboard shows zeros, not errors. Seed a few test executions via playground chat before demoing.
- **CRAG verdict population**: Requires tracing the verdict through the execution pipeline to `RagExecution.Create`. If the plumbing is complex, defer to Phase 2 and just add the column + migration now.
- **Two RagExecution entities**: `KnowledgeBase/Domain/Entities/RagExecution.cs` (this spec) and `Administration/Domain/Aggregates/RagExecution/RagExecution.cs` (replay). Only modify the KnowledgeBase one. Implementation must not confuse the two.
