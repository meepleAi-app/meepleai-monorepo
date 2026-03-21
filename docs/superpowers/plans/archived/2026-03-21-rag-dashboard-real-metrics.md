# RAG Dashboard Real Metrics — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace 4 mocked RAG dashboard query handlers with real DB aggregations over the `RagExecution` table, add `CragVerdict` field and composite index.

**Architecture:** Bottom-up: migration first (schema), then domain models, then repository methods, then handler rewrites, then tests. Single PR to `main-dev`. No DTO or endpoint changes — same contracts, real data instead of mock.

**Tech Stack:** .NET 9, EF Core 9, PostgreSQL 16, MediatR, xUnit, FluentAssertions, Moq

**Spec:** `docs/superpowers/specs/2026-03-21-rag-dashboard-real-metrics-design.md`

---

## Task 1: Add CragVerdict to RagExecution Entity + Migration

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/RagExecution.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Configurations/RagExecutionConfiguration.cs`
- Create: EF Migration (auto-generated)

- [ ] **Step 1: Add CragVerdict property to RagExecution entity**

Open `RagExecution.cs`. Add after the `CacheHit` property (around line 33):

```csharp
public string? CragVerdict { get; private set; }
```

Update the `Create()` factory method to accept an optional parameter. Add `string? cragVerdict = null` as last parameter. Set `CragVerdict = cragVerdict;` in the factory body.

- [ ] **Step 2: Add EF configuration for CragVerdict**

Open `RagExecutionConfiguration.cs`. Add column mapping after the `cache_hit` configuration:

```csharp
builder.Property(e => e.CragVerdict)
    .HasColumnName("crag_verdict")
    .HasMaxLength(20)
    .IsRequired(false);
```

- [ ] **Step 3: Add composite index for dashboard queries**

In the same configuration file, add after existing indexes:

```csharp
builder.HasIndex(e => new { e.Strategy, e.CreatedAt })
    .HasDatabaseName("IX_rag_executions_strategy_created_at")
    .IsDescending(false, true);
```

- [ ] **Step 4: Generate EF migration**

```bash
cd apps/api/src/Api && dotnet ef migrations add AddCragVerdictAndDashboardIndex
```

Review the generated migration to verify it only adds the `crag_verdict` column and the composite index.

- [ ] **Step 5: Build**

```bash
cd apps/api/src/Api && dotnet build
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/RagExecution.cs \
       apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Configurations/RagExecutionConfiguration.cs \
       apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(kb): add CragVerdict field and composite index to RagExecution"
```

---

## Task 2: Create Domain Models (StrategyAggregateMetrics + TimeSeriesPoint)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Models/StrategyAggregateMetrics.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Models/TimeSeriesPoint.cs`

- [ ] **Step 1: Create StrategyAggregateMetrics record**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Models/StrategyAggregateMetrics.cs
namespace Api.BoundedContexts.KnowledgeBase.Domain.Models;

public sealed record StrategyAggregateMetrics(
    string Strategy,
    int TotalQueries,
    double AverageLatencyMs,
    double P95LatencyMs,
    double P99LatencyMs,
    double AverageConfidence,
    int CacheHits,
    int CacheMisses,
    int TotalTokensUsed,           // int — matches StrategyMetricsDto.TotalTokensUsed
    decimal TotalCost,
    decimal AverageCostPerQuery,
    int ErrorCount,
    double ErrorRate,
    int CragCorrect,
    int CragIncorrect,
    int CragAmbiguous,
    DateTimeOffset LastUpdated);    // DateTimeOffset — matches StrategyMetricsDto.LastUpdated
```

- [ ] **Step 2: Create TimeSeriesPoint record and enum**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Models/TimeSeriesPoint.cs
namespace Api.BoundedContexts.KnowledgeBase.Domain.Models;

public sealed record TimeSeriesPoint(
    DateTimeOffset Bucket,          // DateTimeOffset for consistency with DTOs
    int QueryCount,
    double AverageLatencyMs,
    double AverageConfidence,
    decimal TotalCost);

public enum TimeSeriesGranularity { Hour, Day, Week }
```

- [ ] **Step 3: Build**

```bash
cd apps/api/src/Api && dotnet build
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Models/
git commit -m "feat(kb): add StrategyAggregateMetrics and TimeSeriesPoint domain models"
```

---

## Task 3: Extend Repository Interface + Implementation

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Repositories/IRagExecutionRepository.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/RagExecutionRepository.cs`

- [ ] **Step 1: Add methods to IRagExecutionRepository**

Add at the end of the interface, before the closing brace:

```csharp
Task<IReadOnlyList<StrategyAggregateMetrics>> GetAggregatedMetricsAsync(
    DateOnly? startDate, DateOnly? endDate,
    string? strategy = null,
    CancellationToken cancellationToken = default);

Task<IReadOnlyList<TimeSeriesPoint>> GetTimeSeriesMetricsAsync(
    string strategy,
    DateOnly startDate, DateOnly endDate,
    TimeSeriesGranularity granularity,
    CancellationToken cancellationToken = default);
```

Add `using Api.BoundedContexts.KnowledgeBase.Domain.Models;` at the top.

- [ ] **Step 2: Implement GetAggregatedMetricsAsync**

Add to `RagExecutionRepository.cs`. This is the most complex query. Use a two-step approach:
1. EF Core GroupBy for basic aggregates (count, avg, sum)
2. Separate queries for P95/P99 (EF can't do PERCENTILE_CONT)

```csharp
public async Task<IReadOnlyList<StrategyAggregateMetrics>> GetAggregatedMetricsAsync(
    DateOnly? startDate, DateOnly? endDate,
    string? strategy = null,
    CancellationToken cancellationToken = default)
{
    var query = DbContext.Set<RagExecution>().AsNoTracking();

    if (startDate.HasValue)
        query = query.Where(e => e.CreatedAt >= startDate.Value.ToDateTime(TimeOnly.MinValue));
    if (endDate.HasValue)
        query = query.Where(e => e.CreatedAt <= endDate.Value.ToDateTime(TimeOnly.MaxValue));
    if (!string.IsNullOrWhiteSpace(strategy))
        query = query.Where(e => e.Strategy == strategy);

    var groups = await query
        .GroupBy(e => e.Strategy)
        .Select(g => new
        {
            Strategy = g.Key,
            TotalQueries = g.Count(),
            AverageLatencyMs = g.Average(e => (double)e.TotalLatencyMs),
            AverageConfidence = g.Average(e => e.Confidence ?? 0.0),
            CacheHits = g.Count(e => e.CacheHit),
            CacheMisses = g.Count(e => !e.CacheHit),
            TotalTokensUsed = g.Sum(e => e.TotalTokens),
            TotalCost = g.Sum(e => e.TotalCost),
            ErrorCount = g.Count(e => e.Status == "Error"),
            CragCorrect = g.Count(e => e.CragVerdict == "Correct"),
            CragIncorrect = g.Count(e => e.CragVerdict == "Incorrect"),
            CragAmbiguous = g.Count(e => e.CragVerdict == "Ambiguous"),
            LastUpdated = g.Max(e => (DateTimeOffset)e.CreatedAt)
        })
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);

    var results = new List<StrategyAggregateMetrics>();

    foreach (var g in groups)
    {
        // P95/P99: fetch latency values for this strategy and compute
        var latencies = await query
            .Where(e => e.Strategy == g.Strategy)
            .OrderBy(e => e.TotalLatencyMs)
            .Select(e => (double)e.TotalLatencyMs)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var p95 = latencies.Count > 0 ? latencies[(int)(latencies.Count * 0.95)] : 0.0;
        var p99 = latencies.Count > 0 ? latencies[(int)(latencies.Count * 0.99)] : 0.0;

        results.Add(new StrategyAggregateMetrics(
            Strategy: g.Strategy,
            TotalQueries: g.TotalQueries,
            AverageLatencyMs: g.AverageLatencyMs,
            P95LatencyMs: p95,
            P99LatencyMs: p99,
            AverageConfidence: g.AverageConfidence,
            CacheHits: g.CacheHits,
            CacheMisses: g.CacheMisses,
            TotalTokensUsed: g.TotalTokensUsed,
            TotalCost: g.TotalCost,
            AverageCostPerQuery: g.TotalQueries > 0 ? g.TotalCost / g.TotalQueries : 0,
            ErrorCount: g.ErrorCount,
            ErrorRate: g.TotalQueries > 0 ? (double)g.ErrorCount / g.TotalQueries : 0,
            CragCorrect: g.CragCorrect,
            CragIncorrect: g.CragIncorrect,
            CragAmbiguous: g.CragAmbiguous,
            LastUpdated: g.LastUpdated));
    }

    return results;
}
```

- [ ] **Step 3: Implement GetTimeSeriesMetricsAsync**

```csharp
public async Task<IReadOnlyList<TimeSeriesPoint>> GetTimeSeriesMetricsAsync(
    string strategy,
    DateOnly startDate, DateOnly endDate,
    TimeSeriesGranularity granularity,
    CancellationToken cancellationToken = default)
{
    var start = startDate.ToDateTime(TimeOnly.MinValue);
    var end = endDate.ToDateTime(TimeOnly.MaxValue);

    var executions = await DbContext.Set<RagExecution>()
        .AsNoTracking()
        .Where(e => e.Strategy == strategy && e.CreatedAt >= start && e.CreatedAt <= end)
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);

    // Group in memory by truncated bucket (EF can't translate date_trunc)
    var grouped = executions.GroupBy(e => TruncateTimestamp(e.CreatedAt, granularity));

    return grouped
        .Select(g => new TimeSeriesPoint(
            Bucket: new DateTimeOffset(g.Key, TimeSpan.Zero),
            QueryCount: g.Count(),
            AverageLatencyMs: g.Average(e => (double)e.TotalLatencyMs),
            AverageConfidence: g.Average(e => e.Confidence ?? 0.0),
            TotalCost: g.Sum(e => e.TotalCost)))
        .OrderBy(p => p.Bucket)
        .ToList();
}

private static DateTime TruncateTimestamp(DateTime dt, TimeSeriesGranularity granularity)
{
    return granularity switch
    {
        TimeSeriesGranularity.Hour => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, DateTimeKind.Utc),
        TimeSeriesGranularity.Day => new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Utc),
        TimeSeriesGranularity.Week => new DateTime(dt.AddDays(-(int)dt.DayOfWeek).Year, dt.AddDays(-(int)dt.DayOfWeek).Month, dt.AddDays(-(int)dt.DayOfWeek).Day, 0, 0, 0, DateTimeKind.Utc),
        _ => new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Utc)
    };
}
```

- [ ] **Step 4: Build**

```bash
cd apps/api/src/Api && dotnet build
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Repositories/IRagExecutionRepository.cs \
       apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/RagExecutionRepository.cs
git commit -m "feat(kb): implement GetAggregatedMetricsAsync and GetTimeSeriesMetricsAsync"
```

---

## Task 4: Rewrite 4 Dashboard Handlers

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/RagDashboardQueryHandlers.cs`

**IMPORTANT**: Only rewrite handlers 3-6 (GetRagDashboardOverview, GetStrategyMetrics, GetStrategyTimeSeries, GetStrategyComparison). Leave handlers 1-2 (GetRagConfig, GetRagDashboardOptions) untouched.

- [ ] **Step 1: Update handler dependencies**

In `GetRagDashboardOverviewQueryHandler`:
1. Replace `IHybridCacheService` constructor parameter with `IRagExecutionRepository`
2. Store as `_ragExecutionRepository`
3. Add `using Api.BoundedContexts.KnowledgeBase.Domain.Models;`
4. Add `using Api.BoundedContexts.KnowledgeBase.Domain.Enums;` (for `RagStrategy.Parse().GetDisplayName()`)

Do the same for `GetStrategyMetricsQueryHandler`, `GetStrategyTimeSeriesQueryHandler`, `GetStrategyComparisonQueryHandler` — all need `IRagExecutionRepository` instead of `IHybridCacheService`.

- [ ] **Step 2: Rewrite GetRagDashboardOverviewQueryHandler.Handle**

Replace the mock implementation (lines ~210-319) with:

```csharp
public async Task<RagDashboardOverviewDto> Handle(GetRagDashboardOverviewQuery request, CancellationToken cancellationToken)
{
    var endDate = request.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
    var startDate = request.StartDate ?? endDate.AddDays(-30);

    var metrics = await _ragExecutionRepository.GetAggregatedMetricsAsync(
        startDate, endDate, cancellationToken: cancellationToken).ConfigureAwait(false);

    var strategies = metrics.Select(m => MapToStrategyMetricsDto(m)).ToList();

    var aggregated = strategies.Count > 0 ? new StrategyMetricsDto
    {
        StrategyId = "all",
        StrategyName = "All Strategies",
        TotalQueries = strategies.Sum(s => s.TotalQueries),
        AverageLatencyMs = strategies.Average(s => s.AverageLatencyMs),
        P95LatencyMs = strategies.Max(s => s.P95LatencyMs),
        P99LatencyMs = strategies.Max(s => s.P99LatencyMs),
        AverageRelevanceScore = strategies.Average(s => s.AverageRelevanceScore),
        AverageConfidenceScore = strategies.Average(s => s.AverageConfidenceScore),
        CacheHits = strategies.Sum(s => s.CacheHits),
        CacheMisses = strategies.Sum(s => s.CacheMisses),
        CacheHitRate = strategies.Sum(s => s.CacheHits + s.CacheMisses) > 0
            ? (double)strategies.Sum(s => s.CacheHits) / strategies.Sum(s => s.CacheHits + s.CacheMisses) : 0,
        TotalTokensUsed = strategies.Sum(s => s.TotalTokensUsed),
        TotalCost = strategies.Sum(s => s.TotalCost),
        AverageCostPerQuery = strategies.Sum(s => s.TotalQueries) > 0
            ? strategies.Sum(s => s.TotalCost) / strategies.Sum(s => s.TotalQueries) : 0,
        ErrorCount = strategies.Sum(s => s.ErrorCount),
        ErrorRate = strategies.Sum(s => s.TotalQueries) > 0
            ? (double)strategies.Sum(s => s.ErrorCount) / strategies.Sum(s => s.TotalQueries) : 0,
        LastUpdated = strategies.Max(s => s.LastUpdated)
    } : new StrategyMetricsDto { StrategyId = "all", StrategyName = "All Strategies" };

    var best = strategies.OrderByDescending(s => s.AverageConfidenceScore).FirstOrDefault();
    var costEffective = strategies.Where(s => s.TotalQueries > 10)
        .OrderBy(s => s.AverageCostPerQuery).FirstOrDefault();

    return new RagDashboardOverviewDto
    {
        Strategies = strategies,
        AggregatedMetrics = aggregated,
        StartDate = startDate,
        EndDate = endDate,
        BestPerformingStrategy = best?.StrategyId ?? string.Empty,
        MostCostEffectiveStrategy = costEffective?.StrategyId ?? string.Empty
    };
}

private static StrategyMetricsDto MapToStrategyMetricsDto(StrategyAggregateMetrics m)
{
    // Strategy strings in DB (e.g., "Hybrid") may not match RagStrategy enum names (e.g., "Balanced")
    // Use Enum.TryParse with ignoreCase, fall back to raw string if no match
    var strategyName = Enum.TryParse<RagStrategy>(m.Strategy, ignoreCase: true, out var parsed)
        ? parsed.GetDisplayName() : m.Strategy;

    return new StrategyMetricsDto
    {
        StrategyId = m.Strategy,
        StrategyName = strategyName,
        TotalQueries = m.TotalQueries,
        AverageLatencyMs = m.AverageLatencyMs,
        P95LatencyMs = m.P95LatencyMs,
        P99LatencyMs = m.P99LatencyMs,
        AverageRelevanceScore = m.AverageConfidence,
        AverageConfidenceScore = m.AverageConfidence,
        CacheHits = m.CacheHits,
        CacheMisses = m.CacheMisses,
        CacheHitRate = (m.CacheHits + m.CacheMisses) > 0
            ? (double)m.CacheHits / (m.CacheHits + m.CacheMisses) : 0,
        TotalTokensUsed = m.TotalTokensUsed,
        TotalCost = m.TotalCost,
        AverageCostPerQuery = m.AverageCostPerQuery,
        ErrorCount = m.ErrorCount,
        ErrorRate = m.ErrorRate,
        LastUpdated = m.LastUpdated
    };
}
```

- [ ] **Step 3: Rewrite GetStrategyMetricsQueryHandler.Handle**

```csharp
public async Task<StrategyMetricsDto> Handle(GetStrategyMetricsQuery request, CancellationToken cancellationToken)
{
    var endDate = request.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
    var startDate = request.StartDate ?? endDate.AddDays(-30);

    var metrics = await _ragExecutionRepository.GetAggregatedMetricsAsync(
        startDate, endDate, strategy: request.StrategyId, cancellationToken: cancellationToken)
        .ConfigureAwait(false);

    if (metrics.Count == 0)
    {
        return new StrategyMetricsDto
        {
            StrategyId = request.StrategyId,
            StrategyName = RagStrategy.TryParse(request.StrategyId, out var p) ? p.GetDisplayName() : request.StrategyId
        };
    }

    return MapToStrategyMetricsDto(metrics[0]);
}
```

- [ ] **Step 4: Rewrite GetStrategyTimeSeriesQueryHandler.Handle**

```csharp
public async Task<StrategyTimeSeriesMetricsDto> Handle(GetStrategyTimeSeriesQuery request, CancellationToken cancellationToken)
{
    var endDate = request.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
    var startDate = request.StartDate ?? endDate.AddDays(-7);

    var granularity = request.Granularity?.ToLowerInvariant() switch
    {
        "hour" => TimeSeriesGranularity.Hour,
        "week" => TimeSeriesGranularity.Week,
        _ => TimeSeriesGranularity.Day
    };

    var points = await _ragExecutionRepository.GetTimeSeriesMetricsAsync(
        request.StrategyId, startDate, endDate, granularity, cancellationToken)
        .ConfigureAwait(false);

    return new StrategyTimeSeriesMetricsDto
    {
        StrategyId = request.StrategyId,
        LatencyTrend = points.Select(p => new MetricsTimeSeriesPointDto { Timestamp = p.Bucket, Value = p.AverageLatencyMs }).ToList(),
        RelevanceTrend = points.Select(p => new MetricsTimeSeriesPointDto { Timestamp = p.Bucket, Value = p.AverageConfidence }).ToList(),
        QueryCountTrend = points.Select(p => new MetricsTimeSeriesPointDto { Timestamp = p.Bucket, Value = p.QueryCount }).ToList(),
        CostTrend = points.Select(p => new MetricsTimeSeriesPointDto { Timestamp = p.Bucket, Value = (double)p.TotalCost }).ToList()
    };
}
```

- [ ] **Step 5: Rewrite GetStrategyComparisonQueryHandler.Handle**

```csharp
public async Task<StrategyComparisonDto> Handle(GetStrategyComparisonQuery request, CancellationToken cancellationToken)
{
    var endDate = request.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
    var startDate = request.StartDate ?? endDate.AddDays(-30);

    var allMetrics = await _ragExecutionRepository.GetAggregatedMetricsAsync(
        startDate, endDate, cancellationToken: cancellationToken).ConfigureAwait(false);

    // Filter to requested strategies if specified
    var filtered = request.StrategyIds?.Any() == true
        ? allMetrics.Where(m => request.StrategyIds.Contains(m.Strategy)).ToList()
        : allMetrics.ToList();

    var strategies = filtered.Select(m => MapToStrategyMetricsDto(m)).ToList();

    // Rankings as Dictionary<string, double> — value is normalized score (0-1, higher = better)
    var count = (double)strategies.Count;
    var latencyRanking = strategies.OrderBy(s => s.AverageLatencyMs)
        .Select((s, i) => (s.StrategyId, Score: count > 1 ? 1.0 - i / (count - 1) : 1.0))
        .ToDictionary(x => x.StrategyId, x => x.Score) as IReadOnlyDictionary<string, double>;

    var qualityRanking = strategies.OrderByDescending(s => s.AverageConfidenceScore)
        .Select((s, i) => (s.StrategyId, Score: count > 1 ? 1.0 - i / (count - 1) : 1.0))
        .ToDictionary(x => x.StrategyId, x => x.Score) as IReadOnlyDictionary<string, double>;

    var costStrategies = strategies.Where(s => s.TotalQueries > 0).ToList();
    var costCount = (double)costStrategies.Count;
    var costRanking = costStrategies.OrderBy(s => s.AverageCostPerQuery)
        .Select((s, i) => (s.StrategyId, Score: costCount > 1 ? 1.0 - i / (costCount - 1) : 1.0))
        .ToDictionary(x => x.StrategyId, x => x.Score) as IReadOnlyDictionary<string, double>;

    // Weighted recommendation (quality 50%, latency 30%, cost 20%)
    var recommended = string.Empty;
    var reason = string.Empty;
    if (strategies.Count > 0)
    {
        var scored = strategies.Select(s => new
        {
            s.StrategyId,
            Score = (latencyRanking.GetValueOrDefault(s.StrategyId, 0) * 0.3)
                  + (qualityRanking.GetValueOrDefault(s.StrategyId, 0) * 0.5)
                  + (costRanking.GetValueOrDefault(s.StrategyId, 0) * 0.2)
        }).OrderByDescending(s => s.Score).First();

        recommended = scored.StrategyId;
        reason = $"Best overall score ({scored.Score:F2}) based on quality (50%), latency (30%), and cost (20%) weighting";
    }

    return new StrategyComparisonDto
    {
        Strategies = strategies,
        LatencyRanking = latencyRanking,
        QualityRanking = qualityRanking,
        CostEfficiencyRanking = costRanking,
        RecommendedStrategy = recommended,
        RecommendationReason = reason
    };
}
```

- [ ] **Step 6: Remove old mock helper methods**

Delete `CreateMockMetrics()` and any strategy list constants used only by the mock implementation. Keep the `_strategies` list if used by the options handler.

- [ ] **Step 7: Update DI registration if needed**

Check `KnowledgeBaseServiceExtensions.cs` — `IRagExecutionRepository` should already be registered. If the old handlers injected `IHybridCacheService` via DI, no registration change needed (just constructor swap).

- [ ] **Step 8: Build**

```bash
cd apps/api/src/Api && dotnet build
```

Fix any compilation errors (likely: missing usings, property name mismatches on DTOs).

- [ ] **Step 9: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/RagDashboardQueryHandlers.cs
git commit -m "refactor(kb): replace mock dashboard handlers with real RagExecution queries"
```

---

## Task 5: Rewrite Unit Tests

**Files:**
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/RagDashboard/GetRagDashboardOverviewQueryHandlerTests.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/RagDashboard/GetStrategyMetricsQueryHandlerTests.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/RagDashboard/GetStrategyTimeSeriesQueryHandlerTests.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/RagDashboard/GetStrategyComparisonQueryHandlerTests.cs`

- [ ] **Step 1: Rewrite GetRagDashboardOverviewQueryHandlerTests**

Replace mock of `IHybridCacheService` with mock of `IRagExecutionRepository`. Test cases:
- `Handle_WithData_ReturnsStrategyMetrics` — seed 2 strategies with metrics, verify mapping
- `Handle_WithNoData_ReturnsEmptyStrategies` — empty repo returns zeroed aggregated metrics
- `Handle_WithDateRange_PassesDatesToRepository` — verify correct dates forwarded
- `Handle_CalculatesBestPerformingStrategy` — highest confidence wins
- `Handle_CalculatesMostCostEffective` — lowest cost/query with >10 queries wins
- `Handle_MapsConfidenceToBothRelevanceAndConfidenceScores` — verify both fields equal

Each test: mock `GetAggregatedMetricsAsync` to return `List<StrategyAggregateMetrics>`, invoke handler, assert DTO fields.

- [ ] **Step 2: Create GetStrategyMetricsQueryHandlerTests**

Test cases:
- `Handle_ValidStrategy_ReturnsMetrics` — repo returns 1 result, verify mapping
- `Handle_UnknownStrategy_ReturnsEmptyDto` — repo returns empty list
- `Handle_NullDates_DefaultsTo30Days` — verify date defaults

- [ ] **Step 3: Create GetStrategyTimeSeriesQueryHandlerTests**

Test cases:
- `Handle_DailyGranularity_ReturnsBucketedData` — verify time series mapping
- `Handle_HourlyGranularity_ParsesCorrectly` — granularity string → enum
- `Handle_WeeklyGranularity_ParsesCorrectly`
- `Handle_NullGranularity_DefaultsToDaily`
- `Handle_EmptyData_ReturnsEmptyTrends`

- [ ] **Step 4: Create GetStrategyComparisonQueryHandlerTests**

Test cases:
- `Handle_MultipleStrategies_RanksCorrectly` — verify latency/quality/cost rankings
- `Handle_WithStrategyFilter_OnlyIncludesRequested`
- `Handle_WeightedScoring_RecommendsCorrectStrategy` — quality 50% > latency 30% > cost 20%
- `Handle_EmptyData_ReturnsNoRecommendation`

- [ ] **Step 5: Run all tests**

```bash
cd apps/api && dotnet test --filter "BoundedContext=KnowledgeBase&Category=Unit" --no-build
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/RagDashboard/
git commit -m "test(kb): rewrite dashboard handler tests for real metrics + add 3 new test files"
```

---

## Task 6: Full Verification + PR

- [ ] **Step 1: Full build**

```bash
cd apps/api/src/Api && dotnet build
```

- [ ] **Step 2: Run all KnowledgeBase tests**

```bash
cd apps/api && dotnet test --filter "BoundedContext=KnowledgeBase" --no-build
```

- [ ] **Step 3: Verify no mock data references remain**

```bash
grep -rn "CreateMockMetrics\|Random.*42\|FUTURE.*Replace.*actual" apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/RagDashboardQueryHandlers.cs
```

Expected: No results.

- [ ] **Step 4: Push and create PR**

```bash
git push -u origin feature/rag-dashboard-real-metrics
gh pr create --base main-dev --title "feat(kb): replace mock RAG dashboard with real RagExecution metrics (#51)" --body "$(cat <<'EOF'
## Summary

Replaces 4 mocked RAG dashboard query handlers with real EF Core aggregations over the RagExecution table.

### Changes
- Added `CragVerdict` nullable column to RagExecution entity + composite index `(strategy, created_at)`
- Created `StrategyAggregateMetrics` and `TimeSeriesPoint` domain models
- Added `GetAggregatedMetricsAsync` and `GetTimeSeriesMetricsAsync` to IRagExecutionRepository
- Rewrote 4 handlers: Overview, StrategyMetrics, TimeSeries, Comparison
- Same DTOs and endpoints — no frontend changes needed
- Removed all mock data generation (CreateMockMetrics, seeded Random)

### What stays unchanged
- GetRagConfigQueryHandler (already real)
- GetRagDashboardOptionsQueryHandler (hardcoded reference data)
- All endpoint routes and DTO contracts

## Test plan
- [ ] `dotnet build` passes
- [ ] All KnowledgeBase unit tests pass
- [ ] No mock data references in dashboard handlers
- [ ] Dashboard endpoints return empty data gracefully when no executions exist

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 5: Code review**

Run `/code-review:code-review <PR-URL>` before merging.

- [ ] **Step 6: Post-merge cleanup**

```bash
git checkout main-dev && git pull
git branch -D feature/rag-dashboard-real-metrics
git remote prune origin
```
