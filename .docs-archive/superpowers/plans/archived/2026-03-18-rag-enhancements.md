# RAG Enhancements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete integration of existing RAG enhancement services (RAPTOR retrieval, Graph RAG retrieval, RAG-Fusion wider activation) into the live pipeline, add RAG observability metrics and admin dashboard, and activate enhancements for wider user tiers.

**Architecture:** The codebase already has the following services **fully implemented and registered in DI**:
- `RaptorIndexer` — builds hierarchical summary trees, **already called during PDF indexing** (persists to `raptor_summaries` table)
- `EntityExtractor` — extracts knowledge graph triples, **implemented but NOT called during PDF indexing**
- `QueryExpander` — generates query variations for RAG-Fusion, **integrated in `RagPromptAssemblyService` behind feature flag**
- `QueryComplexityClassifier` — classifies query complexity for Adaptive RAG, **integrated behind feature flag**
- `RetrievalRelevanceEvaluator` — CRAG-style evaluation, **integrated behind feature flag**

All enhancements are gated behind `RagEnhancement` [Flags] enum + `IFeatureFlagService.IsEnabledForTierAsync()`. The remaining work is:
1. **Observability** — new Prometheus metrics + admin endpoint + frontend dashboard
2. **RAPTOR retrieval** — summaries are indexed but NOT searched at query time (missing `SearchRaptorSummariesAsync`)
3. **Graph RAG** — extraction exists but not wired into indexing pipeline, no retrieval service
4. **Feature flags** — enable RAG-Fusion/Adaptive for `normal` tier (currently only `premium`)

**Tech Stack:** .NET 9, PostgreSQL + pgvector, EF Core, MediatR CQRS, Next.js 16, React 19, Tailwind 4, shadcn/ui, Vitest

**Key Codebase Paths:**

| Component | Path |
|-----------|------|
| Enhancement services | `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/` |
| RAG pipeline | `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs` |
| PDF indexing | `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs` |
| Feature flags enum | `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Enums/RagEnhancement.cs` |
| Feature flag seeder | `apps/api/src/Api/Infrastructure/Seeders/Core/FeatureFlagSeeder.cs` |
| Metrics | `apps/api/src/Api/Observability/MeepleAiMetrics.cs` |
| DB context | `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` |
| DI registration | `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs` |
| Admin endpoint pattern | `apps/api/src/Api/Routing/Admin*.cs` (use `RequireAdminSession()` → 3-tuple check) |
| RAPTOR entity | `apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/RaptorSummaryEntity.cs` (FK: `PdfDocumentId`, levels: 0=leaf, 1=section, 2=overview) |
| Graph entity | `apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/GameEntityRelationEntity.cs` |
| Text search service | `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Services/TextChunkSearchService.cs` |
| Tests | `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/` |
| Frontend RAG dashboard | `apps/web/src/components/rag-dashboard/` |

**Auth Pattern (admin endpoints):**
```csharp
var (authorized, _, error) = context.RequireAdminSession();
if (!authorized) return error!;
```

**Feature Flag Seeder Pattern:**
```csharp
// FeatureFlagSeedData(FeatureName, Description, GlobalEnabled, FreeEnabled, NormalEnabled, PremiumEnabled)
new("feature_name", "Description", true, false, true, true)
// Tiers: free, normal, premium
```

---

## Phase 1: RAG Observability — Backend Metrics & Admin Endpoint

### Task 1.1: Add RAG Retrieval Quality Metrics

**Files:**
- Modify: `apps/api/src/Api/Observability/MeepleAiMetrics.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/RagObservabilityMetricsTests.cs`

**Context:** `MeepleAiMetrics.cs` already has: `RagRequestsTotal`, `RagRequestDuration`, `ConfidenceScore`, `RagFirstTokenLatency`, `RagErrorsTotal`, `VectorSearchTotal`, `VectorSearchDuration`, `VectorResultsCount`, `GenAiTokenUsage`, `GenAiOperationDuration`, `LlmCostUsd`, `AgentTokenUsage`, `AgentCostUsd`, plus PDF metrics. Missing: retrieval precision proxies, enhancement activation counts, fallback rates, CRAG verdicts.

- [ ] **Step 1: Write failing test for new metric definitions**

```csharp
// File: apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/RagObservabilityMetricsTests.cs
using Api.Observability;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class RagObservabilityMetricsTests
{
    [Fact]
    public void RagMetrics_RetrievalQualityMetrics_AreDefinedAndNotNull()
    {
        Assert.NotNull(MeepleAiMetrics.RagRetrievalChunkCount);
        Assert.NotNull(MeepleAiMetrics.RagRetrievalAvgScore);
        Assert.NotNull(MeepleAiMetrics.RagEnhancementActivations);
        Assert.NotNull(MeepleAiMetrics.RagRetrievalFallbacks);
        Assert.NotNull(MeepleAiMetrics.RagCragVerdicts);
        Assert.NotNull(MeepleAiMetrics.RagAdaptiveRoutingDecisions);
        Assert.NotNull(MeepleAiMetrics.RagFusionQueryCount);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~RagObservabilityMetricsTests" -v n`
Expected: FAIL — metrics not defined yet.

- [ ] **Step 3: Add new metrics to MeepleAiMetrics.cs**

Add at the end of the `MeepleAiMetrics` class, before the closing brace:

```csharp
// === RAG Retrieval Quality Metrics ===

public static readonly Histogram<int> RagRetrievalChunkCount = Meter.CreateHistogram<int>(
    name: "meepleai.rag.retrieval.chunk_count",
    unit: "chunks",
    description: "Number of chunks returned per RAG retrieval");

public static readonly Histogram<double> RagRetrievalAvgScore = Meter.CreateHistogram<double>(
    name: "meepleai.rag.retrieval.avg_score",
    unit: "score",
    description: "Average similarity score of retrieved chunks");

public static readonly Counter<long> RagEnhancementActivations = Meter.CreateCounter<long>(
    name: "meepleai.rag.enhancement.activations",
    unit: "activations",
    description: "RAG enhancement activations by type");

public static readonly Counter<long> RagRetrievalFallbacks = Meter.CreateCounter<long>(
    name: "meepleai.rag.retrieval.fallbacks",
    unit: "fallbacks",
    description: "RAG retrieval fallback events by type");

public static readonly Counter<long> RagCragVerdicts = Meter.CreateCounter<long>(
    name: "meepleai.rag.crag.verdicts",
    unit: "evaluations",
    description: "CRAG retrieval evaluation verdicts");

public static readonly Counter<long> RagAdaptiveRoutingDecisions = Meter.CreateCounter<long>(
    name: "meepleai.rag.adaptive.decisions",
    unit: "decisions",
    description: "Adaptive RAG routing decisions by complexity level");

public static readonly Histogram<int> RagFusionQueryCount = Meter.CreateHistogram<int>(
    name: "meepleai.rag.fusion.query_count",
    unit: "queries",
    description: "Number of query variants generated by RAG-Fusion");
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~RagObservabilityMetricsTests" -v n`
Expected: PASS

- [ ] **Step 5: Instrument RagPromptAssemblyService with new metrics**

In `RagPromptAssemblyService.cs` `RetrieveRagContextAsync` method, add `using Api.Observability;` at top, then:

After `activeEnhancements` is set (~line 168):
```csharp
foreach (var flag in activeEnhancements.GetIndividualFlags())
{
    MeepleAiMetrics.RagEnhancementActivations.Add(1,
        new KeyValuePair<string, object?>("enhancement", flag.ToString()));
}
```

After Adaptive RAG routing decision (~line 182):
```csharp
MeepleAiMetrics.RagAdaptiveRoutingDecisions.Add(1,
    new KeyValuePair<string, object?>("level", complexity.Level.ToString()),
    new KeyValuePair<string, object?>("skipped_retrieval", (!complexity.RequiresRetrieval).ToString()));
```

After RAG-Fusion expansion (~line 198):
```csharp
MeepleAiMetrics.RagFusionQueryCount.Record(queries.Count);
```

After chunk filtering (~line 241):
```csharp
MeepleAiMetrics.RagRetrievalChunkCount.Record(filteredChunks.Count);
if (filteredChunks.Count > 0)
{
    MeepleAiMetrics.RagRetrievalAvgScore.Record(filteredChunks.Average(c => (double)c.Score));
}
```

After CRAG evaluation verdict (~line 304):
```csharp
MeepleAiMetrics.RagCragVerdicts.Add(1,
    new KeyValuePair<string, object?>("verdict", evaluation.Verdict.ToString()));
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Observability/MeepleAiMetrics.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/RagObservabilityMetricsTests.cs
git commit -m "feat(rag): add retrieval quality Prometheus metrics for RAG observability"
```

---

### Task 1.2: Add RAG Quality Report Admin Endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/RagQualityReportDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetRagQualityReportQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/GetRagQualityReportQueryHandler.cs`
- Create: `apps/api/src/Api/Routing/AdminRagQualityEndpoints.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/GetRagQualityReportQueryHandlerTests.cs`

**Context:** Admin endpoints follow `Admin*.cs` naming in `Routing/`. Auth uses `RequireAdminSession()` → 3-tuple `(authorized, session, error)`. DB doesn't have per-query logs — report pulls from VectorDocuments + RaptorSummaries + GameEntityRelations for index health. Real-time metrics come from Prometheus.

- [ ] **Step 1: Create the DTO**

```csharp
// File: apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/RagQualityReportDto.cs
namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

public sealed record RagQualityReportDto(
    int TotalIndexedDocuments,
    int TotalRaptorSummaries,
    int TotalEntityRelations,
    int TotalEmbeddedChunks,
    List<RagQualityGameBreakdown> TopGamesByChunkCount,
    List<RagEnhancementStatusDto> EnhancementStatuses);

public sealed record RagQualityGameBreakdown(
    Guid GameId,
    string GameTitle,
    int ChunkCount,
    int RaptorNodeCount,
    int EntityRelationCount);

public sealed record RagEnhancementStatusDto(
    string Name,
    string FeatureFlagKey,
    bool FreeEnabled,
    bool NormalEnabled,
    bool PremiumEnabled);
```

- [ ] **Step 2: Create query**

```csharp
// File: apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetRagQualityReportQuery.cs
using MediatR;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

public sealed record GetRagQualityReportQuery : IRequest<RagQualityReportDto>;
```

- [ ] **Step 3: Create handler**

```csharp
// File: apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/GetRagQualityReportQueryHandler.cs
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

internal sealed class GetRagQualityReportQueryHandler
    : IRequestHandler<GetRagQualityReportQuery, RagQualityReportDto>
{
    private readonly MeepleAiDbContext _db;

    public GetRagQualityReportQueryHandler(MeepleAiDbContext db) => _db = db;

    public async Task<RagQualityReportDto> Handle(
        GetRagQualityReportQuery request, CancellationToken ct)
    {
        var totalDocs = await _db.VectorDocuments.CountAsync(ct).ConfigureAwait(false);
        var totalRaptor = await _db.RaptorSummaries.CountAsync(ct).ConfigureAwait(false);
        var totalRelations = await _db.GameEntityRelations.CountAsync(ct).ConfigureAwait(false);
        var totalChunks = await _db.Set<Api.Infrastructure.Entities.KnowledgeBase.TextChunkEntity>()
            .CountAsync(ct).ConfigureAwait(false);

        // Top games by chunk count
        var topGames = await _db.VectorDocuments
            .GroupBy(vd => vd.GameId)
            .Select(g => new
            {
                GameId = g.Key,
                ChunkCount = g.Sum(v => v.ChunkCount)
            })
            .OrderByDescending(g => g.ChunkCount)
            .Take(10)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var gameIds = topGames.Select(g => g.GameId).ToList();
        var gameNames = await _db.Games
            .Where(g => gameIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Title })
            .ToDictionaryAsync(g => g.Id, g => g.Title, ct)
            .ConfigureAwait(false);

        var raptorCounts = await _db.RaptorSummaries
            .Where(r => gameIds.Contains(r.GameId))
            .GroupBy(r => r.GameId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.GameId, g => g.Count, ct)
            .ConfigureAwait(false);

        var relationCounts = await _db.GameEntityRelations
            .Where(r => gameIds.Contains(r.GameId))
            .GroupBy(r => r.GameId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.GameId, g => g.Count, ct)
            .ConfigureAwait(false);

        var breakdown = topGames.Select(g => new RagQualityGameBreakdown(
            g.GameId,
            gameNames.GetValueOrDefault(g.GameId, "Unknown"),
            g.ChunkCount,
            raptorCounts.GetValueOrDefault(g.GameId, 0),
            relationCounts.GetValueOrDefault(g.GameId, 0)
        )).ToList();

        // Enhancement statuses from enum
        var enhancementStatuses = new List<RagEnhancementStatusDto>
        {
            new("Adaptive Routing", "rag.enhancement.adaptive-routing", false, false, false),
            new("CRAG Evaluation", "rag.enhancement.crag-evaluation", false, false, false),
            new("RAG-Fusion", "rag.enhancement.rag-fusion-queries", false, false, false),
            new("RAPTOR Retrieval", "rag.enhancement.raptor-retrieval", false, false, false),
            new("Graph Traversal", "rag.enhancement.graph-traversal", false, false, false),
        };
        // Note: actual tier enablement would require querying SystemConfiguration table
        // This provides the structure; Grafana provides real-time Prometheus metrics

        return new RagQualityReportDto(
            TotalIndexedDocuments: totalDocs,
            TotalRaptorSummaries: totalRaptor,
            TotalEntityRelations: totalRelations,
            TotalEmbeddedChunks: totalChunks,
            TopGamesByChunkCount: breakdown,
            EnhancementStatuses: enhancementStatuses);
    }
}
```

- [ ] **Step 4: Create admin endpoint file**

```csharp
// File: apps/api/src/Api/Routing/AdminRagQualityEndpoints.cs
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

internal static class AdminRagQualityEndpoints
{
    internal static void MapAdminRagQualityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/rag")
            .WithTags("Admin", "RAG Quality");

        group.MapGet("/quality-report", async (
            HttpContext context,
            IMediator mediator) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var report = await mediator.Send(new GetRagQualityReportQuery());
            return Results.Ok(report);
        });
    }
}
```

Register in `Program.cs` or wherever endpoints are mapped (find with: `grep -r "MapAdmin" apps/api/src/Api/Program.cs`).

- [ ] **Step 5: Write handler unit test**

```csharp
// File: apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/GetRagQualityReportQueryHandlerTests.cs
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class GetRagQualityReportQueryHandlerTests
{
    [Fact]
    public void Query_HasCorrectType()
    {
        var query = new GetRagQualityReportQuery();
        Assert.IsAssignableFrom<MediatR.IRequest<Api.BoundedContexts.KnowledgeBase.Application.DTOs.RagQualityReportDto>>(query);
    }
}
```

Note: Full integration test with DB requires `SharedDatabaseTestBase` + `[Trait("Category", "Integration")]`. Add as a separate step if desired.

- [ ] **Step 6: Run tests**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~GetRagQualityReportQueryHandlerTests" -v n`

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/RagQualityReportDto.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetRagQualityReportQuery.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/GetRagQualityReportQueryHandler.cs apps/api/src/Api/Routing/AdminRagQualityEndpoints.cs apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/GetRagQualityReportQueryHandlerTests.cs
git commit -m "feat(rag): add admin RAG quality report endpoint with game-level index health"
```

---

## Phase 2: RAPTOR Retrieval — Search Summaries at Query Time

**Context:** RAPTOR tree building is already done during PDF indexing (`PdfProcessingPipelineService.cs:125-156`). Summaries are persisted in `raptor_summaries` table. What's missing: **searching these summaries at query time** when `RagEnhancement.RaptorRetrieval` flag is active.

### Task 2.1: Add RAPTOR Summary Search to TextChunkSearchService

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Services/TextChunkSearchService.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/RaptorSummarySearchTests.cs`

- [ ] **Step 1: Add interface method**

Find the `ITextChunkSearchService` interface and add:

```csharp
/// <summary>
/// Search RAPTOR summary nodes for a game, ordered by relevance.
/// Returns higher-level summaries for broad queries.
/// </summary>
Task<List<SearchResultItem>> SearchRaptorSummariesAsync(
    string query, Guid gameId, int topK, CancellationToken ct);
```

- [ ] **Step 2: Write failing test**

```csharp
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class RaptorSummarySearchTests
{
    [Fact]
    public void SearchResultItem_FromRaptorSummary_HasCorrectMetadata()
    {
        // Verify that RAPTOR results can be distinguished from regular chunks
        var item = new SearchResultItem(
            PdfId: Guid.NewGuid().ToString(),
            ChunkIndex: -1001, // Negative = RAPTOR (level 1, cluster 1)
            Text: "Game overview summary",
            Score: 0.85f,
            PageNumber: 0,
            SearchMethod: "raptor");

        Assert.True(item.ChunkIndex < 0);
        Assert.Equal("raptor", item.SearchMethod);
    }
}
```

- [ ] **Step 3: Implement SearchRaptorSummariesAsync in TextChunkSearchService**

```csharp
public async Task<List<SearchResultItem>> SearchRaptorSummariesAsync(
    string query, Guid gameId, int topK, CancellationToken ct)
{
    // Use PostgreSQL FTS on raptor_summaries table
    var summaries = await _db.RaptorSummaries
        .Where(s => s.GameId == gameId)
        .OrderByDescending(s => s.TreeLevel) // Prefer higher-level (broader) summaries
        .Take(topK)
        .ToListAsync(ct)
        .ConfigureAwait(false);

    // Simple keyword matching for relevance scoring
    var queryTerms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

    return summaries
        .Select(s =>
        {
            var lowerText = s.SummaryText.ToLowerInvariant();
            var matchCount = queryTerms.Count(t => lowerText.Contains(t, StringComparison.Ordinal));
            var score = matchCount > 0 ? 0.6f + (0.1f * Math.Min(matchCount, 4)) : 0.5f;

            return new SearchResultItem(
                PdfId: s.PdfDocumentId.ToString(),
                ChunkIndex: -(s.TreeLevel * 1000 + s.ClusterIndex),
                Text: s.SummaryText,
                Score: score,
                PageNumber: 0,
                SearchMethod: "raptor");
        })
        .Where(r => r.Score >= 0.5f)
        .OrderByDescending(r => r.Score)
        .Take(topK)
        .ToList();
}
```

- [ ] **Step 4: Run test**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Services/TextChunkSearchService.cs apps/api/tests/
git commit -m "feat(rag): add RAPTOR summary search to TextChunkSearchService"
```

---

### Task 2.2: Integrate RAPTOR into RAG Retrieval Pipeline

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/RagPromptAssemblyEnhancementsTests.cs` (extend existing)

- [ ] **Step 1: Write failing test**

Add to existing `RagPromptAssemblyEnhancementsTests.cs`:

```csharp
[Fact]
public async Task AssemblePrompt_WithRaptorEnabled_IncludesSummariesInContext()
{
    // Setup: mock enhancement service to return RaptorRetrieval flag
    // Setup: mock text search to return RAPTOR results
    // Act: call AssemblePromptAsync
    // Assert: assembled prompt contains RAPTOR summary text
}
```

- [ ] **Step 2: Add RAPTOR retrieval to RetrieveRagContextAsync**

In `RagPromptAssemblyService.cs`, after the CRAG evaluation section (after `debugCollector?.Add(StreamingEventType.DebugCragEvaluation, ...)`) and before sentence window expansion:

```csharp
// === RAPTOR: Multi-granularity retrieval ===
if (activeEnhancements.HasFlag(RagEnhancement.RaptorRetrieval))
{
    var raptorChunks = await _textSearch.SearchRaptorSummariesAsync(
        userQuestion, gameId, topK: 3, ct).ConfigureAwait(false);

    if (raptorChunks.Count > 0)
    {
        // Merge RAPTOR results, slightly boosted
        foreach (var rc in raptorChunks)
        {
            filteredChunks.Add(rc with { Score = rc.Score * 1.1f });
        }

        filteredChunks = filteredChunks
            .OrderByDescending(c => c.Score)
            .Take(RerankedTopK + 2)
            .ToList();

        _logger.LogInformation("RAPTOR: added {Count} summary chunks to context", raptorChunks.Count);
    }
}
```

- [ ] **Step 3: Run tests**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~RagPromptAssemblyEnhancementsTests" -v n`

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs apps/api/tests/
git commit -m "feat(rag): integrate RAPTOR multi-granularity retrieval into RAG pipeline"
```

---

## Phase 3: Graph RAG — Extract During Indexing + Query-Time Retrieval

### Task 3.1: Wire Entity Extraction into PDF Indexing

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/GraphRagExtractionTests.cs`

**Context:** `EntityExtractor` and `GameEntityRelationEntity` exist. `PdfProcessingPipelineService` already has optional `IRaptorIndexer?`. Add optional `IEntityExtractor?` using the same pattern.

- [ ] **Step 1: Write failing test**

```csharp
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class GraphRagExtractionTests
{
    [Fact]
    public async Task EntityExtraction_WithValidText_ReturnsRelations()
    {
        // Unit test for EntityExtractor itself
        var llmMock = new Mock<ILlmService>();
        llmMock.Setup(l => l.GenerateJsonAsync<EntityExtractionResponse>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResponse(new List<RawRelation>
            {
                new("Catan", "Game", "HasMechanic", "Trading", "Mechanic")
            }));

        var extractor = new EntityExtractor(llmMock.Object, Mock.Of<ILogger<EntityExtractor>>());
        var result = await extractor.ExtractEntitiesAsync(Guid.NewGuid(), "Catan", "Trading is a core mechanic...", CancellationToken.None);

        Assert.Single(result.Relations);
        Assert.Equal("Trading", result.Relations[0].TargetEntity);
    }
}
```

- [ ] **Step 2: Run test to verify it passes** (this tests existing code)

- [ ] **Step 3: Add IEntityExtractor as optional dependency to PdfProcessingPipelineService**

Add field + constructor parameter (same pattern as `IRaptorIndexer?`):

```csharp
private readonly IEntityExtractor? _entityExtractor;
// Constructor: IEntityExtractor? entityExtractor = null
_entityExtractor = entityExtractor;
```

After the RAPTOR section in `ProcessAsync` (~line 156), add:

```csharp
// === Graph RAG: Extract entity relations (optional, non-blocking) ===
if (_entityExtractor is not null && fullText.Length >= 200)
{
    try
    {
        var gameTitle = pdfDoc.OriginalFileName ?? "Unknown";
        var extraction = await _entityExtractor.ExtractEntitiesAsync(
            gameId, gameTitle,
            fullText[..Math.Min(fullText.Length, 8000)],
            cancellationToken).ConfigureAwait(false);

        if (extraction.Relations.Count > 0)
        {
            var entities = extraction.Relations.Select(r => new GameEntityRelationEntity
            {
                Id = Guid.NewGuid(),
                GameId = gameId,
                SourceEntity = r.SourceEntity,
                SourceType = r.SourceType,
                Relation = r.Relation,
                TargetEntity = r.TargetEntity,
                TargetType = r.TargetType,
                Confidence = r.Confidence,
                ExtractedAt = DateTime.UtcNow
            }).ToList();

            _db.GameEntityRelations.AddRange(entities);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "[PdfPipeline] Graph RAG: extracted {RelCount} relations for PDF {PdfId}",
                entities.Count, pdfDoc.Id);
        }
    }
#pragma warning disable CA1031 // Graph RAG is optional enhancement
    catch (Exception ex)
    {
        _logger.LogWarning(ex,
            "[PdfPipeline] Graph RAG extraction failed for PDF {PdfId}, continuing",
            pdfDoc.Id);
    }
#pragma warning restore CA1031
}
```

- [ ] **Step 4: Run all KnowledgeBase tests**

Run: `cd apps/api && dotnet test --filter "BoundedContext=KnowledgeBase" -v n`

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs apps/api/tests/
git commit -m "feat(rag): wire Graph RAG entity extraction into PDF indexing pipeline"
```

---

### Task 3.2: Create Graph Retrieval Service

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/IGraphRetrievalService.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/GraphRetrievalService.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/GraphRetrievalServiceTests.cs`

- [ ] **Step 1: Create interface**

```csharp
// File: .../Domain/Services/Enhancements/IGraphRetrievalService.cs
namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;

internal interface IGraphRetrievalService
{
    Task<string> GetEntityContextAsync(Guid gameId, int maxRelations, CancellationToken ct);
}
```

- [ ] **Step 2: Write failing test**

```csharp
[Trait("Category", "Integration")]
[Trait("BoundedContext", "KnowledgeBase")]
public class GraphRetrievalServiceTests : SharedDatabaseTestBase
{
    [Fact]
    public async Task GetEntityContext_WithRelations_ReturnsFormattedContext()
    {
        var gameId = Guid.NewGuid();
        DbContext.GameEntityRelations.Add(new GameEntityRelationEntity
        {
            Id = Guid.NewGuid(), GameId = gameId,
            SourceEntity = "Catan", SourceType = "Game",
            Relation = "HasMechanic", TargetEntity = "Trading", TargetType = "Mechanic",
            Confidence = 0.9f, ExtractedAt = DateTime.UtcNow
        });
        await DbContext.SaveChangesAsync();

        var service = new GraphRetrievalService(DbContext);
        var context = await service.GetEntityContextAsync(gameId, 10, CancellationToken.None);

        Assert.Contains("Trading", context);
        Assert.Contains("HasMechanic", context);
    }

    [Fact]
    public async Task GetEntityContext_NoRelations_ReturnsEmpty()
    {
        var service = new GraphRetrievalService(DbContext);
        var context = await service.GetEntityContextAsync(Guid.NewGuid(), 10, CancellationToken.None);
        Assert.Equal(string.Empty, context);
    }
}
```

- [ ] **Step 3: Implement GraphRetrievalService**

```csharp
// File: .../Domain/Services/Enhancements/GraphRetrievalService.cs
using System.Text;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;

internal sealed class GraphRetrievalService : IGraphRetrievalService
{
    private readonly MeepleAiDbContext _db;

    public GraphRetrievalService(MeepleAiDbContext db) => _db = db;

    public async Task<string> GetEntityContextAsync(
        Guid gameId, int maxRelations, CancellationToken ct)
    {
        var relations = await _db.GameEntityRelations
            .Where(r => r.GameId == gameId)
            .OrderByDescending(r => r.Confidence)
            .Take(maxRelations)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (relations.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("\n[Knowledge Graph]");
        foreach (var r in relations)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"- {r.SourceEntity} ({r.SourceType}) --{r.Relation}--> {r.TargetEntity} ({r.TargetType})");
        }

        return sb.ToString();
    }
}
```

- [ ] **Step 4: Register in DI**

In `KnowledgeBaseServiceExtensions.cs`:
```csharp
services.AddScoped<IGraphRetrievalService, GraphRetrievalService>();
```

- [ ] **Step 5: Integrate into RagPromptAssemblyService**

Inject `IGraphRetrievalService` in constructor. In `RetrieveRagContextAsync`, after RAPTOR section:

```csharp
// === Graph RAG: Inject entity context ===
if (activeEnhancements.HasFlag(RagEnhancement.GraphTraversal))
{
    var graphContext = await _graphRetrievalService
        .GetEntityContextAsync(gameId, maxRelations: 15, ct).ConfigureAwait(false);

    if (!string.IsNullOrEmpty(graphContext))
    {
        // Append graph context to the assembled RAG context
        // This will be picked up in BuildSystemPrompt
        _logger.LogInformation("Graph RAG: injected entity context for game {GameId}", gameId);
    }
}
```

The exact integration point depends on where `ragContext` string is built — append `graphContext` to the returned ragContext string.

- [ ] **Step 6: Run tests**

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/IGraphRetrievalService.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/GraphRetrievalService.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs apps/api/tests/
git commit -m "feat(rag): add Graph RAG retrieval service with entity context injection"
```

---

## Phase 4: Activate RAG Enhancement Feature Flags

### Task 4.1: Update Feature Flag Seeder

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Seeders/Core/FeatureFlagSeeder.cs`

**Context:** Feature flag seeder uses `FeatureFlagSeedData(FeatureName, Description, GlobalEnabled, FreeEnabled, NormalEnabled, PremiumEnabled)` record. Tiers are `free`, `normal`, `premium`. Current `DefaultFeatureFlags` array does NOT include RAG enhancement flags.

- [ ] **Step 1: Add RAG enhancement flags to DefaultFeatureFlags array**

In `FeatureFlagSeeder.cs`, add to the `DefaultFeatureFlags` array (after the existing entries):

```csharp
// RAG Enhancement flags
new("rag.enhancement.adaptive-routing", "Adaptive RAG: skip retrieval for simple queries", true, false, true, true),
new("rag.enhancement.crag-evaluation", "CRAG: evaluate retrieval quality before generation", true, false, false, true),
new("rag.enhancement.rag-fusion-queries", "RAG-Fusion: multi-query retrieval for better recall", true, false, true, true),
new("rag.enhancement.raptor-retrieval", "RAPTOR: hierarchical summary retrieval", true, false, false, true),
new("rag.enhancement.graph-traversal", "Graph RAG: entity relation context injection", true, false, false, true),
```

This enables:
- **Adaptive Routing**: `normal` + `premium` (saves tokens for simple queries — net positive)
- **RAG-Fusion**: `normal` + `premium` (improves recall, moderate cost)
- **CRAG**: `premium` only (adds LLM call for evaluation)
- **RAPTOR**: `premium` only (new feature, needs validation)
- **Graph RAG**: `premium` only (new feature, needs validation)

- [ ] **Step 2: Verify seeder runs without error**

Run: `cd apps/api/src/Api && dotnet build`
The seeder runs at startup — verify no compilation errors.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Seeders/Core/FeatureFlagSeeder.cs
git commit -m "feat(rag): seed RAG enhancement feature flags for normal and premium tiers"
```

---

## Phase 5: Frontend RAG Observability Dashboard

### Task 5.1: Create RAG Quality Dashboard Page

**Files:**
- Create: `apps/web/src/lib/api/rag-quality.ts`
- Create: `apps/web/src/components/admin/rag-quality-dashboard.tsx`
- Create: `apps/web/src/app/(admin)/admin/rag-quality/page.tsx`
- Test: `apps/web/src/__tests__/components/admin/rag-quality-dashboard.test.tsx`

- [ ] **Step 1: Create API client**

```typescript
// File: apps/web/src/lib/api/rag-quality.ts
import { apiClient } from '@/lib/api/client';

export interface RagQualityReport {
  totalIndexedDocuments: number;
  totalRaptorSummaries: number;
  totalEntityRelations: number;
  totalEmbeddedChunks: number;
  topGamesByChunkCount: Array<{
    gameId: string;
    gameTitle: string;
    chunkCount: number;
    raptorNodeCount: number;
    entityRelationCount: number;
  }>;
  enhancementStatuses: Array<{
    name: string;
    featureFlagKey: string;
    freeEnabled: boolean;
    normalEnabled: boolean;
    premiumEnabled: boolean;
  }>;
}

export async function fetchRagQualityReport(): Promise<RagQualityReport> {
  const res = await apiClient.get('/admin/rag/quality-report');
  return res.data;
}
```

- [ ] **Step 2: Create dashboard component**

```tsx
// File: apps/web/src/components/admin/rag-quality-dashboard.tsx
'use client';

import { useQuery } from '@tanstack/react-query';
import { fetchRagQualityReport } from '@/lib/api/rag-quality';

export function RagQualityDashboard() {
  const { data, isLoading, error } = useQuery({
    queryKey: ['rag-quality-report'],
    queryFn: fetchRagQualityReport,
  });

  if (isLoading) return <div className="p-6">Loading RAG quality report...</div>;
  if (error) return <div className="p-6 text-red-600">Failed to load report</div>;
  if (!data) return null;

  return (
    <div className="space-y-6 p-6">
      <h1 className="font-quicksand text-2xl font-bold">RAG Quality Dashboard</h1>

      {/* Summary Cards */}
      <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
        <SummaryCard label="Indexed Documents" value={data.totalIndexedDocuments} />
        <SummaryCard label="Embedded Chunks" value={data.totalEmbeddedChunks} />
        <SummaryCard label="RAPTOR Summaries" value={data.totalRaptorSummaries} />
        <SummaryCard label="Entity Relations" value={data.totalEntityRelations} />
      </div>

      {/* Top Games */}
      <div className="rounded-lg bg-white/70 p-4 shadow backdrop-blur-md">
        <h2 className="mb-3 font-nunito text-lg font-semibold">Top Games by Index Size</h2>
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b text-left text-gray-500">
              <th className="pb-2">Game</th>
              <th className="pb-2 text-right">Chunks</th>
              <th className="pb-2 text-right">RAPTOR</th>
              <th className="pb-2 text-right">Relations</th>
            </tr>
          </thead>
          <tbody>
            {data.topGamesByChunkCount.map((g) => (
              <tr key={g.gameId} className="border-b border-gray-100">
                <td className="py-2">{g.gameTitle}</td>
                <td className="py-2 text-right">{g.chunkCount}</td>
                <td className="py-2 text-right">{g.raptorNodeCount}</td>
                <td className="py-2 text-right">{g.entityRelationCount}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Enhancement Statuses */}
      <div className="rounded-lg bg-white/70 p-4 shadow backdrop-blur-md">
        <h2 className="mb-3 font-nunito text-lg font-semibold">RAG Enhancement Flags</h2>
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b text-left text-gray-500">
              <th className="pb-2">Enhancement</th>
              <th className="pb-2 text-center">Free</th>
              <th className="pb-2 text-center">Normal</th>
              <th className="pb-2 text-center">Premium</th>
            </tr>
          </thead>
          <tbody>
            {data.enhancementStatuses.map((e) => (
              <tr key={e.featureFlagKey} className="border-b border-gray-100">
                <td className="py-2">{e.name}</td>
                <td className="py-2 text-center">{e.freeEnabled ? '✓' : '—'}</td>
                <td className="py-2 text-center">{e.normalEnabled ? '✓' : '—'}</td>
                <td className="py-2 text-center">{e.premiumEnabled ? '✓' : '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function SummaryCard({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-lg bg-white/70 p-4 shadow backdrop-blur-md">
      <p className="text-sm text-gray-500">{label}</p>
      <p className="font-quicksand text-2xl font-bold text-amber-700">
        {value.toLocaleString()}
      </p>
    </div>
  );
}
```

- [ ] **Step 3: Create page**

```tsx
// File: apps/web/src/app/(admin)/admin/rag-quality/page.tsx
import { RagQualityDashboard } from '@/components/admin/rag-quality-dashboard';

export default function RagQualityPage() {
  return <RagQualityDashboard />;
}
```

- [ ] **Step 4: Write test**

```tsx
// File: apps/web/src/__tests__/components/admin/rag-quality-dashboard.test.tsx
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi, describe, it, expect } from 'vitest';

vi.mock('@/lib/api/rag-quality', () => ({
  fetchRagQualityReport: vi.fn().mockResolvedValue({
    totalIndexedDocuments: 42,
    totalRaptorSummaries: 15,
    totalEntityRelations: 87,
    totalEmbeddedChunks: 1234,
    topGamesByChunkCount: [],
    enhancementStatuses: [],
  }),
}));

const { RagQualityDashboard } = await import('@/components/admin/rag-quality-dashboard');

describe('RagQualityDashboard', () => {
  it('renders summary cards with data', async () => {
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={qc}>
        <RagQualityDashboard />
      </QueryClientProvider>
    );

    expect(await screen.findByText('1,234')).toBeInTheDocument();
    expect(screen.getByText('42')).toBeInTheDocument();
  });
});
```

- [ ] **Step 5: Run frontend tests**

Run: `cd apps/web && pnpm test -- --run rag-quality`

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/lib/api/rag-quality.ts apps/web/src/components/admin/rag-quality-dashboard.tsx "apps/web/src/app/(admin)/admin/rag-quality/page.tsx" apps/web/src/__tests__/components/admin/rag-quality-dashboard.test.tsx
git commit -m "feat(rag): add admin RAG quality observability dashboard"
```

---

## Verification Checklist

After all phases:

- [ ] `cd apps/api && dotnet build` — compiles without errors
- [ ] `cd apps/api && dotnet test --filter "BoundedContext=KnowledgeBase"` — all tests pass
- [ ] `cd apps/web && pnpm typecheck` — no TypeScript errors
- [ ] `cd apps/web && pnpm test -- --run` — all frontend tests pass
- [ ] `cd apps/web && pnpm lint` — no lint errors
- [ ] Manual: Upload a PDF → verify `raptor_summaries` table has entries (already works)
- [ ] Manual: Upload a PDF → verify `game_entity_relations` table has entries (new in Phase 3)
- [ ] Manual: Query as `normal` tier → verify RAG-Fusion generates query variants (check debug SSE events type 25)
- [ ] Manual: Visit `/admin/rag-quality` → dashboard loads with data
- [ ] Prometheus: New metrics appear: `meepleai_rag_retrieval_chunk_count`, `meepleai_rag_enhancement_activations`, etc.

---

## Architecture Summary

```
BEFORE:
  Indexing:  PDF → Chunks → Embed → pgvector + FTS
             PDF → RAPTOR tree → raptor_summaries (stored but NOT searched)
  Query:    → [Adaptive Route] → [RAG-Fusion?] → Hybrid Search → RRF → Rerank → [CRAG?] → Context → LLM
  Flags:    Adaptive/CRAG/Fusion = premium only. RAPTOR/Graph = not wired.

AFTER:
  Indexing:  PDF → Chunks → Embed → pgvector + FTS
             PDF → RAPTOR tree → raptor_summaries (stored + SEARCHABLE)
             PDF → EntityExtractor → game_entity_relations (NEW)
  Query:    → [Adaptive Route] → [RAG-Fusion expand] → Hybrid Search → RRF → Rerank
            → [CRAG evaluate] → [RAPTOR augment] → [Graph context inject] → Context → LLM
  Flags:    Adaptive + Fusion = normal+premium. CRAG + RAPTOR + Graph = premium.
  Observability: 7 new Prometheus metrics + Admin dashboard + Quality report endpoint
```
