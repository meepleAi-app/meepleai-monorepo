# Qdrant → pgvector-only Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove all Qdrant code, types, and infrastructure from MeepleAI while wiring the admin Vector Collections page to real pgvector stats and semantic search.

**Architecture:** Backend gains two new CQRS queries (`GetVectorStats`, `VectorSemanticSearch`) under the KnowledgeBase BC. StorageHealth is refactored to rename `QdrantInfoDto` → `VectorStoreInfoDto`. Frontend vectors page is rewritten around the new endpoints; all Qdrant strings/types are deleted project-wide.

**Tech Stack:** .NET 9 (MediatR, EF Core, InMemory for tests), Next.js 16 (React Query, Zod, Vitest), xUnit + Moq + FluentAssertions

---

## File Map

### Create
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetVectorStatsQuery.cs`
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetVectorStatsQueryHandler.cs`
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/VectorSemanticSearchQuery.cs`
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/VectorSemanticSearchQueryHandler.cs`
- `apps/web/src/components/admin/knowledge-base/vector-game-card.tsx`
- `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/GetVectorStatsQueryHandlerTests.cs`
- `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/VectorSemanticSearchQueryHandlerTests.cs`

### Modify
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetPdfStorageHealthQuery.cs`
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetPdfStorageHealthQueryHandler.cs`
- `apps/api/src/Api/Routing/AdminPipelineEndpoints.cs`
- `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs`
- `apps/api/src/Api/Routing/MonitoringEndpoints.cs`
- `apps/api/src/Api/Routing/AdminSecretsEndpoints.cs`
- `apps/web/src/lib/api/schemas/admin-knowledge-base.schemas.ts`
- `apps/web/src/lib/api/clients/admin/adminAiClient.ts`
- `apps/web/src/lib/api/clients/adminClient.ts`
- `apps/web/src/app/admin/(dashboard)/knowledge-base/vectors/page.tsx`
- `apps/web/src/components/admin/knowledge-base/kb-settings.tsx`
- `apps/web/src/components/rag-dashboard/ArchitectureExplorer.tsx`
- `apps/web/src/components/rag-dashboard/PocStatus.tsx`
- `apps/web/src/components/rag-dashboard/TechnicalReference.tsx`
- `apps/web/src/components/rag-dashboard/rag-data.ts`
- `apps/web/src/components/rag-dashboard/retrieval-strategies/strategy-details-data.ts`
- `apps/web/src/components/rag-dashboard/DecisionWalkthrough.tsx`
- `apps/web/src/components/rag-dashboard/metrics/TokenFlowVisualizer.tsx`
- `apps/web/src/components/rag-dashboard/StrategyFlowVisualizer.tsx`
- `apps/web/src/components/admin/command-center/CommandCenterDashboard.tsx`
- `apps/web/src/components/admin/ServiceStatusCard.tsx`
- `apps/web/src/app/admin/(dashboard)/knowledge-base/documents/page.tsx`
- `apps/web/src/app/admin/(dashboard)/knowledge-base/page.tsx`
- `apps/web/src/app/admin/(dashboard)/ai/RagTab.tsx`
- `apps/web/src/app/admin/(dashboard)/knowledge-base/__tests__/kb-hub-gaps.test.tsx`
- `apps/web/src/app/admin/(dashboard)/monitor/services/__tests__/ServicesDashboard.test.tsx`
- `apps/web/src/app/admin/(dashboard)/monitor/operations/__tests__/ResourcesTab.test.tsx`
- `apps/web/src/components/admin/__tests__/ServiceCard.test.tsx`
- `apps/web/src/components/admin/__tests__/ServiceHealthMatrix.test.tsx`
- `apps/web/src/components/admin/__tests__/SystemStatus.test.tsx`
- `apps/web/src/components/rag-dashboard/__tests__/ArchitectureExplorer.test.tsx`
- `infra/prometheus/alerts/api-performance.yml`
- `infra/prometheus/alerts/quality-metrics.yml`
- `infra/prometheus-rules.yml`
- `infra/alertmanager.yml`

### Delete
- `apps/web/src/components/admin/knowledge-base/vector-collection-card.tsx`
- `infra/secrets/qdrant.secret`

---

## Task 1: StorageHealth refactoring — rename QdrantInfoDto → VectorStoreInfoDto

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetPdfStorageHealthQuery.cs`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetPdfStorageHealthQueryHandler.cs`
- Modify: `apps/api/src/Api/Routing/AdminPipelineEndpoints.cs`

- [ ] **Step 1: Write the failing build check**

Run:
```bash
cd apps/api/src/Api && dotnet build --no-restore 2>&1 | grep -c "error CS"
```
Expected: `0` errors currently.

- [ ] **Step 2: Rename QdrantInfoDto → VectorStoreInfoDto in GetPdfStorageHealthQuery.cs**

Replace the full file content:

```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to get PDF storage health across PostgreSQL, vector store (pgvector), and file storage.
/// PDF Storage Management Hub: Composes existing resource metrics.
/// </summary>
internal record GetPdfStorageHealthQuery() : IQuery<PdfStorageHealthDto>;

internal record PdfStorageHealthDto(
    PostgresInfoDto Postgres,
    VectorStoreInfoDto VectorStore,
    FileStorageInfoDto FileStorage,
    string OverallHealth,
    DateTime MeasuredAt
);

internal record PostgresInfoDto(
    int TotalDocuments,
    int TotalChunks,
    double EstimatedChunksSizeMB
);

internal record VectorStoreInfoDto(
    long VectorCount,
    bool IsAvailable
);

internal record FileStorageInfoDto(
    int TotalFiles,
    long TotalSizeBytes,
    string TotalSizeFormatted,
    Dictionary<string, int> SizeByState
);
```

- [ ] **Step 3: Update GetPdfStorageHealthQueryHandler.cs — populate VectorStore with real data**

Replace the full file content:

```csharp
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Handler for GetPdfStorageHealthQuery.
/// PDF Storage Management Hub: Composes PG + file storage + pgvector metrics.
/// </summary>
internal sealed class GetPdfStorageHealthQueryHandler
    : IQueryHandler<GetPdfStorageHealthQuery, PdfStorageHealthDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public GetPdfStorageHealthQueryHandler(
        MeepleAiDbContext dbContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<PdfStorageHealthDto> Handle(
        GetPdfStorageHealthQuery request, CancellationToken cancellationToken)
    {
        // PostgreSQL metrics
        var totalDocuments = await _dbContext.PdfDocuments
            .CountAsync(cancellationToken).ConfigureAwait(false);

        var totalChunks = await _dbContext.TextChunks
            .CountAsync(cancellationToken).ConfigureAwait(false);

        var totalChunkChars = await _dbContext.TextChunks
            .SumAsync(tc => (long)tc.CharacterCount, cancellationToken).ConfigureAwait(false);

        var estimatedChunksSizeMB = (totalChunkChars * 2.0) / (1024.0 * 1024.0);
        var postgres = new PostgresInfoDto(totalDocuments, totalChunks, Math.Round(estimatedChunksSizeMB, 1));

        // pgvector metrics — real count from vector_documents
        var vectorCount = await _dbContext.VectorDocuments
            .LongCountAsync(cancellationToken).ConfigureAwait(false);
        var vectorStore = new VectorStoreInfoDto(vectorCount, IsAvailable: true);

        // File storage metrics
        var totalSizeBytes = await _dbContext.PdfDocuments
            .SumAsync(p => p.FileSizeBytes, cancellationToken).ConfigureAwait(false);

        var sizeByState = await _dbContext.PdfDocuments
            .GroupBy(p => p.ProcessingState)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.State, x => x.Count, cancellationToken)
            .ConfigureAwait(false);

        var fileStorage = new FileStorageInfoDto(
            TotalFiles: totalDocuments,
            TotalSizeBytes: totalSizeBytes,
            TotalSizeFormatted: FormatBytes(totalSizeBytes),
            SizeByState: sizeByState
        );

        var overallHealth = "healthy";
        if (totalDocuments > 0)
        {
            sizeByState.TryGetValue("Failed", out var failedCount);
            if (failedCount > 0 && (double)failedCount / totalDocuments > 0.1)
                overallHealth = "warning";
        }

        return new PdfStorageHealthDto(
            Postgres: postgres,
            VectorStore: vectorStore,
            FileStorage: fileStorage,
            OverallHealth: overallHealth,
            MeasuredAt: _timeProvider.GetUtcNow().DateTime
        );
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
```

- [ ] **Step 4: Update AdminPipelineEndpoints.cs — replace storageHealth.Qdrant.* with VectorStore.***

In `BuildIndexStage`, replace:
```csharp
    private static object BuildIndexStage(PdfStorageHealthDto storageHealth)
    {
        var status = storageHealth.Qdrant.IsAvailable ? "healthy" : "error";
        return new
        {
            name = "Index",
            status,
            metrics = new
            {
                vectorCount = storageHealth.Qdrant.VectorCount,
                memoryFormatted = storageHealth.Qdrant.MemoryFormatted,
                isAvailable = storageHealth.Qdrant.IsAvailable,
            },
        };
    }
```
With:
```csharp
    private static object BuildIndexStage(PdfStorageHealthDto storageHealth)
    {
        var status = storageHealth.VectorStore.IsAvailable ? "healthy" : "error";
        return new
        {
            name = "Index",
            status,
            metrics = new
            {
                vectorCount = storageHealth.VectorStore.VectorCount,
                isAvailable = storageHealth.VectorStore.IsAvailable,
            },
        };
    }
```

In `BuildRetrieveStage`, replace:
```csharp
    private static object BuildRetrieveStage(Dictionary<string, ServiceHealthStatus> healthByName)
    {
        var qdrantHealth = healthByName.GetValueOrDefault("qdrant");
        string status;
        if (qdrantHealth is null || qdrantHealth.State == HealthState.Unhealthy)
        {
            status = "error";
        }
        else if (qdrantHealth.State == HealthState.Degraded)
        {
            status = "warning";
        }
        else
        {
            status = "healthy";
        }

        return new
        {
            name = "Retrieve",
            status,
            metrics = new
            {
                qdrantHealth = qdrantHealth?.State.ToString() ?? "unknown",
            },
        };
    }
```
With:
```csharp
    private static object BuildRetrieveStage(Dictionary<string, ServiceHealthStatus> healthByName)
    {
        // pgvector is embedded in PostgreSQL — health tied to postgres service
        var pgHealth = healthByName.GetValueOrDefault("postgres");
        string status;
        if (pgHealth is null || pgHealth.State == HealthState.Unhealthy)
            status = "error";
        else if (pgHealth.State == HealthState.Degraded)
            status = "warning";
        else
            status = "healthy";

        return new
        {
            name = "Retrieve",
            status,
            metrics = new
            {
                pgvectorHealth = pgHealth?.State.ToString() ?? "unknown",
            },
        };
    }
```

In the `distribution` anonymous object at line ~125, replace:
```csharp
            vectorCount = storageHealth.Qdrant.VectorCount,
```
With:
```csharp
            vectorCount = storageHealth.VectorStore.VectorCount,
```

- [ ] **Step 5: Verify build passes**

Run:
```bash
cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5
```
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetPdfStorageHealthQuery.cs
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetPdfStorageHealthQueryHandler.cs
git add apps/api/src/Api/Routing/AdminPipelineEndpoints.cs
git commit -m "refactor(kb): rename QdrantInfoDto → VectorStoreInfoDto, populate with real pgvector data"
```

---

## Task 2: Backend cleanup — remove Qdrant from routing files

**Files:**
- Modify: `apps/api/src/Api/Routing/MonitoringEndpoints.cs`
- Modify: `apps/api/src/Api/Routing/AdminSecretsEndpoints.cs`

- [ ] **Step 1: Remove qdrant health endpoint from MonitoringEndpoints.cs**

In `MapServiceHealthEndpoints`, remove the qdrant line:
```csharp
        MapGenericServiceHealthEndpoint(group, "/health/qdrant", "qdrant", "GetQdrantHealth", "Qdrant");
```
(Leave the postgresql, redis, and n8n lines.)

- [ ] **Step 2: Remove qdrant.secret from AdminSecretsEndpoints.cs**

Replace:
```csharp
    private static readonly HashSet<string> InfraFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "database.secret", "redis.secret", "qdrant.secret", "jwt.secret", "admin.secret"
    };
```
With:
```csharp
    private static readonly HashSet<string> InfraFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "database.secret", "redis.secret", "jwt.secret", "admin.secret"
    };
```

Remove the qdrant entry from `CategoryMap`:
```csharp
        ["qdrant.secret"] = "Qdrant",
```
(Delete that line from the dictionary initializer.)

- [ ] **Step 3: Verify build**

```bash
cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -3
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Routing/MonitoringEndpoints.cs
git add apps/api/src/Api/Routing/AdminSecretsEndpoints.cs
git commit -m "chore(routing): remove qdrant health endpoint and qdrant.secret references"
```

---

## Task 3: GetVectorStatsQuery + Handler + backend endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetVectorStatsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetVectorStatsQueryHandler.cs`
- Modify: `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/GetVectorStatsQueryHandlerTests.cs`

- [ ] **Step 1: Write the failing test**

Create `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/GetVectorStatsQueryHandlerTests.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public sealed class GetVectorStatsQueryHandlerTests
{
    private static MeepleAiDbContext CreateDb() =>
        TestDbContextFactory.CreateInMemoryDbContext();

    [Fact]
    public async Task Handle_EmptyDb_ReturnsZeroStats()
    {
        // Arrange
        using var db = CreateDb();
        var handler = new GetVectorStatsQueryHandler(db);

        // Act
        var result = await handler.Handle(new GetVectorStatsQuery(), CancellationToken.None);

        // Assert
        result.TotalVectors.Should().Be(0);
        result.GamesIndexed.Should().Be(0);
        result.Dimensions.Should().Be(768);
        result.AvgHealthPercent.Should().Be(0);
        result.GameBreakdown.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithCompletedDocs_ReturnsRealCounts()
    {
        // Arrange
        using var db = CreateDb();
        var gameId = Guid.NewGuid();

        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = gameId,
            ChunkCount = 50,
            IndexingStatus = "completed",
        });
        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = gameId,
            ChunkCount = 30,
            IndexingStatus = "completed",
        });
        await db.SaveChangesAsync();

        var handler = new GetVectorStatsQueryHandler(db);

        // Act
        var result = await handler.Handle(new GetVectorStatsQuery(), CancellationToken.None);

        // Assert
        result.TotalVectors.Should().Be(80);
        result.GamesIndexed.Should().Be(1);
        result.AvgHealthPercent.Should().Be(100);
        result.GameBreakdown.Should().HaveCount(1);
        result.GameBreakdown[0].VectorCount.Should().Be(80);
        result.GameBreakdown[0].HealthPercent.Should().Be(100);
    }

    [Fact]
    public async Task Handle_WithMixedStatus_ComputesHealthCorrectly()
    {
        // Arrange
        using var db = CreateDb();
        var gameId = Guid.NewGuid();

        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(), SharedGameId = gameId, ChunkCount = 80, IndexingStatus = "completed",
        });
        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(), SharedGameId = gameId, ChunkCount = 20, IndexingStatus = "failed",
        });
        await db.SaveChangesAsync();

        var handler = new GetVectorStatsQueryHandler(db);

        // Act
        var result = await handler.Handle(new GetVectorStatsQuery(), CancellationToken.None);

        // Assert
        result.TotalVectors.Should().Be(100);
        result.GamesIndexed.Should().Be(1);
        result.AvgHealthPercent.Should().Be(80); // 80/100 = 80%
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/api && dotnet test --filter "GetVectorStatsQueryHandlerTests" --no-build 2>&1 | tail -10
```
Expected: FAIL (type not found)

- [ ] **Step 3: Create GetVectorStatsQuery.cs**

```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

internal record GetVectorStatsQuery() : IQuery<VectorStatsDto>;

internal record VectorStatsDto(
    long TotalVectors,
    int Dimensions,
    int GamesIndexed,
    int AvgHealthPercent,
    long SizeEstimateBytes,
    List<VectorGameBreakdownDto> GameBreakdown
);

internal record VectorGameBreakdownDto(
    Guid GameId,
    string GameName,
    long VectorCount,
    long CompletedCount,
    long FailedCount,
    int HealthPercent
);
```

- [ ] **Step 4: Create GetVectorStatsQueryHandler.cs**

```csharp
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Returns aggregated pgvector stats: total vectors, games indexed, health %, per-game breakdown.
/// Queries vector_documents grouped by SharedGameId, joined to shared_game_catalog for names.
/// </summary>
internal sealed class GetVectorStatsQueryHandler
    : IQueryHandler<GetVectorStatsQuery, VectorStatsDto>
{
    private const int EmbeddingDimensions = 768;
    // 768 floats × 4 bytes each
    private const long BytesPerVector = 768 * 4;

    private readonly MeepleAiDbContext _dbContext;

    public GetVectorStatsQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<VectorStatsDto> Handle(
        GetVectorStatsQuery request, CancellationToken cancellationToken)
    {
        // Group vector_documents by SharedGameId
        var groups = await _dbContext.VectorDocuments
            .Where(vd => vd.SharedGameId.HasValue)
            .GroupBy(vd => vd.SharedGameId!.Value)
            .Select(g => new
            {
                GameId = g.Key,
                TotalChunks = g.Sum(vd => (long)vd.ChunkCount),
                CompletedChunks = g.Where(vd => vd.IndexingStatus == "completed").Sum(vd => (long)vd.ChunkCount),
                FailedChunks = g.Where(vd => vd.IndexingStatus == "failed").Sum(vd => (long)vd.ChunkCount),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (groups.Count == 0)
        {
            return new VectorStatsDto(0, EmbeddingDimensions, 0, 0, 0, []);
        }

        // Fetch game names from shared_game_catalog
        var gameIds = groups.Select(g => g.GameId).ToList();
        var gameNames = await _dbContext.SharedGames
            .Where(sg => gameIds.Contains(sg.Id))
            .Select(sg => new { sg.Id, sg.Title })
            .ToDictionaryAsync(sg => sg.Id, sg => sg.Title, cancellationToken)
            .ConfigureAwait(false);

        var breakdown = groups.Select(g =>
        {
            var healthPct = g.TotalChunks > 0
                ? (int)Math.Round(g.CompletedChunks * 100.0 / g.TotalChunks)
                : 0;
            return new VectorGameBreakdownDto(
                GameId: g.GameId,
                GameName: gameNames.GetValueOrDefault(g.GameId, "Unknown"),
                VectorCount: g.TotalChunks,
                CompletedCount: g.CompletedChunks,
                FailedCount: g.FailedChunks,
                HealthPercent: healthPct
            );
        }).ToList();

        var totalVectors = breakdown.Sum(b => b.VectorCount);
        var gamesIndexed = breakdown.Count(b => b.CompletedCount > 0);
        var avgHealth = breakdown.Count > 0
            ? (int)Math.Round(breakdown.Average(b => b.HealthPercent))
            : 0;

        return new VectorStatsDto(
            TotalVectors: totalVectors,
            Dimensions: EmbeddingDimensions,
            GamesIndexed: gamesIndexed,
            AvgHealthPercent: avgHealth,
            SizeEstimateBytes: totalVectors * BytesPerVector,
            GameBreakdown: breakdown
        );
    }
}
```

- [ ] **Step 5: Add GET /vector-stats endpoint to AdminKnowledgeBaseEndpoints.cs**

After the `/vector-collections` endpoint (line ~29), add:

```csharp
        // GET /api/v1/admin/kb/vector-stats
        kbGroup.MapGet("/vector-stats", async (
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new GetVectorStatsQuery(), cancellationToken)
                .ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetVectorStats")
        .WithSummary("Get pgvector stats: total vectors, games indexed, health %, per-game breakdown");
```

Add the missing using at the top of the file:
```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
```

- [ ] **Step 6: Run tests**

```bash
cd apps/api && dotnet test --filter "GetVectorStatsQueryHandlerTests" 2>&1 | tail -10
```
Expected: all 3 tests PASS

- [ ] **Step 7: Verify build**

```bash
cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -3
```
Expected: `Build succeeded.`

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetVectorStatsQuery.cs
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetVectorStatsQueryHandler.cs
git add apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/GetVectorStatsQueryHandlerTests.cs
git commit -m "feat(kb): add GET /api/v1/admin/kb/vector-stats endpoint with real pgvector stats"
```

---

## Task 4: VectorSemanticSearchQuery + Handler + backend endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/VectorSemanticSearchQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/VectorSemanticSearchQueryHandler.cs`
- Modify: `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/VectorSemanticSearchQueryHandlerTests.cs`

- [ ] **Step 1: Write the failing test**

Create `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/VectorSemanticSearchQueryHandlerTests.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public sealed class VectorSemanticSearchQueryHandlerTests
{
    private static float[] FakeVector(int dims = 768) =>
        Enumerable.Range(0, dims).Select(i => (float)i / dims).ToArray();

    [Fact]
    public async Task Handle_EmbeddingFails_ReturnsEmptyResults()
    {
        // Arrange
        var embeddingService = new Mock<IEmbeddingService>();
        embeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Service unavailable"));

        var vectorStore = new Mock<IVectorStoreAdapter>();
        var handler = new VectorSemanticSearchQueryHandler(embeddingService.Object, vectorStore.Object);

        // Act
        var result = await handler.Handle(
            new VectorSemanticSearchQuery("test query", 10, null),
            CancellationToken.None);

        // Assert
        result.Results.Should().BeEmpty();
        result.ErrorMessage.Should().Contain("Service unavailable");
    }

    [Fact]
    public async Task Handle_WithGameId_CallsSearchAsync()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var embeddingService = new Mock<IEmbeddingService>();
        embeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess([FakeVector()]));

        var vectorStore = new Mock<IVectorStoreAdapter>();
        vectorStore
            .Setup(v => v.SearchAsync(gameId, It.IsAny<Vector>(), 10, 0.0, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = new VectorSemanticSearchQueryHandler(embeddingService.Object, vectorStore.Object);

        // Act
        var result = await handler.Handle(
            new VectorSemanticSearchQuery("regole commercio", 10, gameId),
            CancellationToken.None);

        // Assert
        vectorStore.Verify(
            v => v.SearchAsync(gameId, It.IsAny<Vector>(), 10, 0.0, null, It.IsAny<CancellationToken>()),
            Times.Once);
        result.Results.Should().BeEmpty();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ResultsReturned_MapsPayloadCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var embeddingId = Guid.NewGuid();
        var placeholderVector = Vector.CreatePlaceholder(768);

        var embedding = new Embedding(
            id: embeddingId,
            vectorDocumentId: docId,
            textContent: "Regola del commercio: scambia risorse...",
            vector: placeholderVector,
            model: "e5-base",
            chunkIndex: 3,
            pageNumber: 5);

        var embeddingService = new Mock<IEmbeddingService>();
        embeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess([FakeVector()]));

        var vectorStore = new Mock<IVectorStoreAdapter>();
        vectorStore
            .Setup(v => v.SearchAsync(gameId, It.IsAny<Vector>(), 5, 0.0, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([embedding]);

        var handler = new VectorSemanticSearchQueryHandler(embeddingService.Object, vectorStore.Object);

        // Act
        var result = await handler.Handle(
            new VectorSemanticSearchQuery("commercio", 5, gameId),
            CancellationToken.None);

        // Assert
        result.Results.Should().HaveCount(1);
        result.Results[0].Text.Should().Be("Regola del commercio: scambia risorse...");
        result.Results[0].DocumentId.Should().Be(docId);
        result.Results[0].ChunkIndex.Should().Be(3);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/api && dotnet test --filter "VectorSemanticSearchQueryHandlerTests" --no-build 2>&1 | tail -5
```
Expected: FAIL (type not found)

- [ ] **Step 3: Create VectorSemanticSearchQuery.cs**

```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

internal record VectorSemanticSearchQuery(
    string Query,
    int Limit,
    Guid? GameId
) : IQuery<VectorSemanticSearchResultDto>;

internal record VectorSemanticSearchResultDto(
    List<VectorSearchResultItem> Results,
    string? ErrorMessage
);

internal record VectorSearchResultItem(
    Guid DocumentId,
    string Text,
    int ChunkIndex,
    int PageNumber
);
```

- [ ] **Step 4: Create VectorSemanticSearchQueryHandler.cs**

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Admin semantic search via pgvector.
/// Generates an embedding for the query string, then calls IVectorStoreAdapter.SearchAsync.
/// If no gameId is provided, searches across all indexed games.
/// </summary>
internal sealed class VectorSemanticSearchQueryHandler
    : IQueryHandler<VectorSemanticSearchQuery, VectorSemanticSearchResultDto>
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreAdapter _vectorStore;
    private readonly MeepleAiDbContext? _dbContext;

    public VectorSemanticSearchQueryHandler(
        IEmbeddingService embeddingService,
        IVectorStoreAdapter vectorStore,
        MeepleAiDbContext? dbContext = null)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _dbContext = dbContext;
    }

    public async Task<VectorSemanticSearchResultDto> Handle(
        VectorSemanticSearchQuery request, CancellationToken cancellationToken)
    {
        // 1. Generate query embedding
        var embeddingResult = await _embeddingService
            .GenerateEmbeddingAsync(request.Query, cancellationToken)
            .ConfigureAwait(false);

        if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
        {
            return new VectorSemanticSearchResultDto([], embeddingResult.ErrorMessage ?? "Embedding generation failed");
        }

        var queryVector = new Vector(embeddingResult.Embeddings[0]);

        // 2. Search: gameId-scoped or across all indexed games
        List<Domain.Entities.Embedding> hits;

        if (request.GameId.HasValue)
        {
            hits = await _vectorStore
                .SearchAsync(request.GameId.Value, queryVector, request.Limit, 0.0, null, cancellationToken)
                .ConfigureAwait(false);
        }
        else if (_dbContext is not null)
        {
            // Global search: get all distinct game IDs that have indexed vectors
            var gameIds = await _dbContext.VectorDocuments
                .Where(vd => vd.GameId.HasValue && vd.IndexingStatus == "completed")
                .Select(vd => vd.GameId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            hits = gameIds.Count == 0
                ? []
                : await _vectorStore
                    .SearchByMultipleGameIdsAsync(gameIds, queryVector, request.Limit, 0.0, null, cancellationToken)
                    .ConfigureAwait(false);
        }
        else
        {
            hits = [];
        }

        var results = hits.Select(h => new VectorSearchResultItem(
            DocumentId: h.VectorDocumentId,
            Text: h.TextContent,
            ChunkIndex: h.ChunkIndex,
            PageNumber: h.PageNumber
        )).ToList();

        return new VectorSemanticSearchResultDto(results, null);
    }
}
```

- [ ] **Step 5: Add POST /vector-search to AdminKnowledgeBaseEndpoints.cs**

After the `/vector-stats` endpoint, add:

```csharp
        // POST /api/v1/admin/kb/vector-search
        kbGroup.MapPost("/vector-search", async (
            [FromBody] VectorSearchRequest request,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var query = new VectorSemanticSearchQuery(
                Query: request.Query,
                Limit: request.Limit ?? 10,
                GameId: request.GameId
            );
            var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("VectorSemanticSearch")
        .WithSummary("Run semantic search over pgvector embeddings");
```

Add the request record at the bottom of the file (or in a nearby DTO file):

```csharp
internal record VectorSearchRequest(
    string Query,
    int? Limit,
    Guid? GameId
);
```

- [ ] **Step 6: Run tests**

```bash
cd apps/api && dotnet test --filter "VectorSemanticSearchQueryHandlerTests" 2>&1 | tail -10
```
Expected: all 3 tests PASS

- [ ] **Step 7: Verify build**

```bash
cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -3
```

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/VectorSemanticSearchQuery.cs
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/VectorSemanticSearchQueryHandler.cs
git add apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/VectorSemanticSearchQueryHandlerTests.cs
git commit -m "feat(kb): add POST /api/v1/admin/kb/vector-search via pgvector"
```

---

## Task 5: Frontend — update schemas and adminAiClient

**Files:**
- Modify: `apps/web/src/lib/api/schemas/admin-knowledge-base.schemas.ts`
- Modify: `apps/web/src/lib/api/clients/admin/adminAiClient.ts`
- Modify: `apps/web/src/lib/api/clients/adminClient.ts`

- [ ] **Step 1: Update admin-knowledge-base.schemas.ts**

Replace `QdrantInfoSchema` and `PdfStorageHealthSchema` with:

```typescript
export const VectorStoreInfoSchema = z.object({
  vectorCount: z.number(),
  isAvailable: z.boolean(),
});

export const FileStorageInfoSchema = z.object({
  totalFiles: z.number(),
  totalSizeBytes: z.number(),
  totalSizeFormatted: z.string(),
  sizeByState: z.record(z.string(), z.number()),
});

export const PdfStorageHealthSchema = z.object({
  postgres: PostgresInfoSchema,
  vectorStore: VectorStoreInfoSchema,
  fileStorage: FileStorageInfoSchema,
  overallHealth: z.string(),
  measuredAt: z.string(),
});

export type PdfStorageHealth = z.infer<typeof PdfStorageHealthSchema>;
```

Also add the new vector stats schemas at the end of the file:

```typescript
// ========== Vector Stats (pgvector) ==========

export const VectorGameBreakdownSchema = z.object({
  gameId: z.string(),
  gameName: z.string(),
  vectorCount: z.number(),
  completedCount: z.number(),
  failedCount: z.number(),
  healthPercent: z.number(),
});

export const VectorStatsSchema = z.object({
  totalVectors: z.number(),
  dimensions: z.number(),
  gamesIndexed: z.number(),
  avgHealthPercent: z.number(),
  sizeEstimateBytes: z.number(),
  gameBreakdown: z.array(VectorGameBreakdownSchema),
});

export type VectorGameBreakdown = z.infer<typeof VectorGameBreakdownSchema>;
export type VectorStats = z.infer<typeof VectorStatsSchema>;

// ========== Vector Search ==========

export const VectorSearchResultItemSchema = z.object({
  documentId: z.string(),
  text: z.string(),
  chunkIndex: z.number(),
  pageNumber: z.number(),
});

export const VectorSemanticSearchResultSchema = z.object({
  results: z.array(VectorSearchResultItemSchema),
  errorMessage: z.string().nullable(),
});

export type VectorSearchResultItem = z.infer<typeof VectorSearchResultItemSchema>;
export type VectorSemanticSearchResult = z.infer<typeof VectorSemanticSearchResultSchema>;
```

Remove the old `QdrantInfoSchema` export and `type` aliases for it.

- [ ] **Step 2: Update adminAiClient.ts — remove Qdrant, add pgvector methods**

Remove the entire `// ========== Qdrant Admin Types (Issue #4877) ==========` section and all 5 Qdrant interfaces:
`QdrantCollectionDetails`, `QdrantSearchResultItem`, `QdrantSearchResult`, `QdrantBrowsePoint`, `QdrantBrowseResult`

Remove these methods from the `createAdminAiClient` function:
- `getQdrantCollectionDetails`
- `deleteQdrantCollection`
- `searchQdrantCollection`
- `browseQdrantPoints`
- `deleteQdrantPoints`
- `rebuildQdrantIndex`

Remove `getVectorCollections` (the stub returning empty collections).

Update `ADMIN_KB_ROUTES`:
```typescript
export const ADMIN_KB_ROUTES = {
  vectorStats: '/api/v1/admin/kb/vector-stats',
  vectorSearch: '/api/v1/admin/kb/vector-search',
  processingQueue: '/api/v1/admin/kb/processing-queue',
} as const;
```

Add these imports at the top of the file (after existing schema imports):
```typescript
import {
  VectorStatsSchema,
  VectorSemanticSearchResultSchema,
  type VectorStats,
  type VectorSemanticSearchResult,
} from '../../schemas/admin-knowledge-base.schemas';
```

Add these methods inside `createAdminAiClient` (in the RAG/KB/Vector Operations section):

```typescript
    async getVectorStats(): Promise<VectorStats | null> {
      return http.get('/api/v1/admin/kb/vector-stats', VectorStatsSchema);
    },

    async searchVectors(
      query: string,
      limit: number = 10,
      gameId?: string
    ): Promise<VectorSemanticSearchResult> {
      const result = await http.post<VectorSemanticSearchResult>(
        '/api/v1/admin/kb/vector-search',
        { query, limit, gameId: gameId ?? null },
        VectorSemanticSearchResultSchema
      );
      return result ?? { results: [], errorMessage: null };
    },
```

- [ ] **Step 3: Update adminClient.ts — remove Qdrant re-exports**

Open `apps/web/src/lib/api/clients/adminClient.ts` and remove these re-export lines:
```typescript
  QdrantCollectionDetails,
  QdrantSearchResultItem,
  QdrantSearchResult,
  QdrantBrowsePoint,
  QdrantBrowseResult,
```

Add re-exports for the new types:
```typescript
export type {
  VectorStats,
  VectorGameBreakdown,
  VectorSemanticSearchResult,
  VectorSearchResultItem,
} from '../schemas/admin-knowledge-base.schemas';
```

- [ ] **Step 4: Verify TypeScript compilation**

```bash
cd apps/web && pnpm typecheck 2>&1 | head -20
```
Expected: 0 errors in the modified files (may still have errors from other files using Qdrant types — those are fixed in later tasks).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/api/schemas/admin-knowledge-base.schemas.ts
git add apps/web/src/lib/api/clients/admin/adminAiClient.ts
git add apps/web/src/lib/api/clients/adminClient.ts
git commit -m "feat(fe): replace Qdrant API client with pgvector getVectorStats/searchVectors"
```

---

## Task 6: Frontend — new vector-game-card.tsx component

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/vector-game-card.tsx`
- Delete: `apps/web/src/components/admin/knowledge-base/vector-collection-card.tsx`

- [ ] **Step 1: Create vector-game-card.tsx**

```tsx
'use client';

import { CheckCircle2Icon, AlertTriangleIcon, GamepadIcon } from 'lucide-react';

import type { VectorGameBreakdown } from '@/lib/api/clients/adminClient';

interface VectorGameCardProps {
  game: VectorGameBreakdown;
}

export function VectorGameCard({ game }: VectorGameCardProps) {
  const healthColor =
    game.healthPercent >= 90
      ? 'text-emerald-600 dark:text-emerald-400'
      : game.healthPercent >= 70
        ? 'text-yellow-600 dark:text-yellow-400'
        : 'text-red-600 dark:text-red-400';

  const StatusIcon =
    game.healthPercent >= 90 ? CheckCircle2Icon : AlertTriangleIcon;

  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-2xl border border-white/40 dark:border-zinc-700/40 p-5 shadow-sm">
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-2 min-w-0">
          <GamepadIcon className="size-4 shrink-0 text-indigo-500" />
          <h3 className="font-semibold text-sm truncate text-slate-800 dark:text-zinc-100">
            {game.gameName}
          </h3>
        </div>
        <StatusIcon className={`size-4 shrink-0 ${healthColor}`} />
      </div>

      <div className="mt-4 grid grid-cols-3 gap-3 text-center">
        <div>
          <p className="text-lg font-bold text-slate-900 dark:text-zinc-50">
            {game.vectorCount.toLocaleString()}
          </p>
          <p className="text-xs text-slate-500 dark:text-zinc-400">Vectors</p>
        </div>
        <div>
          <p className={`text-lg font-bold ${healthColor}`}>
            {game.healthPercent}%
          </p>
          <p className="text-xs text-slate-500 dark:text-zinc-400">Health</p>
        </div>
        <div>
          <p className="text-lg font-bold text-slate-900 dark:text-zinc-50">
            {game.failedCount > 0 ? game.failedCount.toLocaleString() : '—'}
          </p>
          <p className="text-xs text-slate-500 dark:text-zinc-400">Failed</p>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Delete vector-collection-card.tsx**

```bash
rm "apps/web/src/components/admin/knowledge-base/vector-collection-card.tsx"
```

- [ ] **Step 3: Verify TypeScript (this file only)**

```bash
cd apps/web && npx tsc --noEmit --skipLibCheck 2>&1 | grep "vector-game-card" | head -5
```
Expected: no errors for the new file.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/vector-game-card.tsx
git rm apps/web/src/components/admin/knowledge-base/vector-collection-card.tsx
git commit -m "feat(fe): add VectorGameCard, remove VectorCollectionCard (Qdrant)"
```

---

## Task 7: Frontend — rewrite vectors/page.tsx

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/vectors/page.tsx`

- [ ] **Step 1: Rewrite the file**

Replace the entire file content:

```tsx
'use client';

import { useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import {
  DatabaseIcon,
  BoxesIcon,
  ActivityIcon,
  HardDriveIcon,
  SearchIcon,
} from 'lucide-react';

import { VectorGameCard } from '@/components/admin/knowledge-base/vector-game-card';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';
import type { VectorSearchResultItem } from '@/lib/api/clients/adminClient';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

function StatCard({
  icon: Icon,
  label,
  value,
}: {
  icon: React.ElementType;
  label: string;
  value: string | number;
}) {
  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-white/40 dark:border-zinc-700/40">
      <div className="flex items-center gap-2 text-slate-500 dark:text-zinc-400 mb-1">
        <Icon className="size-4" />
        <span className="text-xs font-medium">{label}</span>
      </div>
      <p className="text-2xl font-bold text-slate-900 dark:text-zinc-50">{value}</p>
    </div>
  );
}

function StatSkeleton() {
  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-white/40 dark:border-zinc-700/40">
      <div className="h-4 w-24 bg-slate-200 dark:bg-zinc-700 rounded animate-pulse mb-2" />
      <div className="h-8 w-16 bg-slate-200 dark:bg-zinc-700 rounded animate-pulse" />
    </div>
  );
}

function CardSkeleton() {
  return (
    <div className="h-[140px] bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse" />
  );
}

export default function VectorStatsPage() {
  const [searchQuery, setSearchQuery] = useState('');
  const [searchGameId, setSearchGameId] = useState('');
  const [searchLimit, setSearchLimit] = useState('10');
  const [searchResults, setSearchResults] = useState<VectorSearchResultItem[] | null>(null);
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [expandedResult, setExpandedResult] = useState<number | null>(null);

  const { data, isLoading, error } = useQuery({
    queryKey: ['admin', 'vector-stats'],
    queryFn: () => adminClient.getVectorStats(),
    staleTime: 60_000,
  });

  const handleSearch = async () => {
    if (!searchQuery.trim()) return;
    setSearchLoading(true);
    setSearchError(null);
    setSearchResults(null);
    try {
      const result = await adminClient.searchVectors(
        searchQuery.trim(),
        parseInt(searchLimit, 10),
        searchGameId || undefined
      );
      if (result.errorMessage) {
        setSearchError(result.errorMessage);
      } else {
        setSearchResults(result.results);
      }
    } catch (err) {
      setSearchError(err instanceof Error ? err.message : 'Search failed');
    } finally {
      setSearchLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-slate-900 dark:text-zinc-50">
          Vector Store
        </h1>
        <p className="text-sm text-slate-500 dark:text-zinc-400 mt-1">
          pgvector embeddings — stats, game breakdown, and semantic search
        </p>
      </div>

      {/* Stats cards */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
        {isLoading ? (
          Array.from({ length: 4 }).map((_, i) => <StatSkeleton key={i} />)
        ) : error || !data ? (
          <p className="col-span-4 text-sm text-red-500">Failed to load vector stats.</p>
        ) : (
          <>
            <StatCard icon={DatabaseIcon} label="Total Vectors" value={data.totalVectors.toLocaleString()} />
            <StatCard icon={BoxesIcon} label="Games Indexed" value={data.gamesIndexed} />
            <StatCard icon={HardDriveIcon} label="Dimensions" value={data.dimensions} />
            <StatCard icon={ActivityIcon} label="Avg Health" value={`${data.avgHealthPercent}%`} />
          </>
        )}
      </div>

      {/* Semantic Search */}
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-2xl border border-white/40 dark:border-zinc-700/40 p-5">
        <h2 className="text-base font-semibold text-slate-800 dark:text-zinc-100 mb-4">
          Semantic Search
        </h2>
        <div className="flex flex-col sm:flex-row gap-3">
          <Input
            placeholder="Search query..."
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handleSearch()}
            className="flex-1"
          />
          {data?.gameBreakdown && data.gameBreakdown.length > 0 && (
            <Select value={searchGameId} onValueChange={setSearchGameId}>
              <SelectTrigger className="w-48">
                <SelectValue placeholder="All games" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="">All games</SelectItem>
                {data.gameBreakdown.map(g => (
                  <SelectItem key={g.gameId} value={g.gameId}>
                    {g.gameName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
          <Select value={searchLimit} onValueChange={setSearchLimit}>
            <SelectTrigger className="w-24">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {['5', '10', '20', '50'].map(n => (
                <SelectItem key={n} value={n}>
                  Top {n}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button
            onClick={handleSearch}
            disabled={!searchQuery.trim() || searchLoading}
          >
            <SearchIcon className="size-4 mr-2" />
            {searchLoading ? 'Searching…' : 'Search'}
          </Button>
        </div>

        {searchError && (
          <p className="mt-3 text-sm text-red-500">{searchError}</p>
        )}

        {searchResults !== null && (
          <div className="mt-4 space-y-2">
            {searchResults.length === 0 ? (
              <p className="text-sm text-slate-500 dark:text-zinc-400">No results found.</p>
            ) : (
              searchResults.map((r, i) => (
                <div
                  key={i}
                  className="border border-slate-200 dark:border-zinc-700 rounded-lg p-3 cursor-pointer hover:bg-slate-50 dark:hover:bg-zinc-700/50 transition-colors"
                  onClick={() => setExpandedResult(expandedResult === i ? null : i)}
                >
                  <div className="flex items-center justify-between gap-2">
                    <span className="text-xs font-mono text-slate-400 dark:text-zinc-500">
                      #{i + 1} · chunk {r.chunkIndex} · p.{r.pageNumber}
                    </span>
                    <span className="text-xs text-slate-400 dark:text-zinc-500">
                      {expandedResult === i ? '▲' : '▼'}
                    </span>
                  </div>
                  {expandedResult === i && (
                    <p className="mt-2 text-sm text-slate-700 dark:text-zinc-300 whitespace-pre-wrap">
                      {r.text}
                    </p>
                  )}
                  {expandedResult !== i && (
                    <p className="mt-1 text-sm text-slate-600 dark:text-zinc-400 truncate">
                      {r.text}
                    </p>
                  )}
                </div>
              ))
            )}
          </div>
        )}
      </div>

      {/* Game cards */}
      <div>
        <h2 className="text-base font-semibold text-slate-800 dark:text-zinc-100 mb-3">
          Indexed Games
        </h2>
        {isLoading ? (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
            {Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}
          </div>
        ) : !data || data.gameBreakdown.length === 0 ? (
          <p className="text-sm text-slate-500 dark:text-zinc-400">
            No games indexed yet. Upload and process PDFs to populate the vector store.
          </p>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
            {data.gameBreakdown.map(game => (
              <VectorGameCard key={game.gameId} game={game} />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Verify TypeScript**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep "vectors/page" | head -5
```
Expected: no errors for the page.

- [ ] **Step 3: Commit**

```bash
git add "apps/web/src/app/admin/(dashboard)/knowledge-base/vectors/page.tsx"
git commit -m "feat(fe): rewrite vectors page for pgvector — stats, game cards, semantic search"
```

---

## Task 8: Frontend — kb-settings, RAG dashboard, monitor, docs cleanup

**Files:**
- Modify: `apps/web/src/components/admin/knowledge-base/kb-settings.tsx`
- Modify: `apps/web/src/components/rag-dashboard/ArchitectureExplorer.tsx`
- Modify: `apps/web/src/components/rag-dashboard/PocStatus.tsx`
- Modify: `apps/web/src/components/rag-dashboard/TechnicalReference.tsx`
- Modify: `apps/web/src/components/rag-dashboard/rag-data.ts`
- Modify: `apps/web/src/components/rag-dashboard/retrieval-strategies/strategy-details-data.ts`
- Modify: `apps/web/src/components/rag-dashboard/DecisionWalkthrough.tsx`
- Modify: `apps/web/src/components/rag-dashboard/metrics/TokenFlowVisualizer.tsx`
- Modify: `apps/web/src/components/rag-dashboard/StrategyFlowVisualizer.tsx`
- Modify: `apps/web/src/components/admin/command-center/CommandCenterDashboard.tsx`
- Modify: `apps/web/src/components/admin/ServiceStatusCard.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/documents/page.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/page.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/ai/RagTab.tsx`

- [ ] **Step 1: kb-settings.tsx — remove Rebuild Qdrant section and Qdrant stats display**

In `kb-settings.tsx`, remove the rebuild section. Find and remove the block starting at line ~141:
```tsx
      // Rebuild all Qdrant collections
```
through to the end of the mutation and its UI (the "Rebuild all Qdrant collections" card).

Also find and replace:
```tsx
              <span className="text-muted-foreground">Qdrant:</span>
              <span>
                {storageHealth.qdrant.isAvailable
                  ? `${storageHealth.qdrant.vectorCount.toLocaleString()} vectors (${storageHealth.qdrant.memoryFormatted})`
                  : 'Unavailable'}
              </span>
```
With:
```tsx
              <span className="text-muted-foreground">Vector Store:</span>
              <span>
                {storageHealth.vectorStore.isAvailable
                  ? `${storageHealth.vectorStore.vectorCount.toLocaleString()} vectors`
                  : 'Unavailable'}
              </span>
```

- [ ] **Step 2: ArchitectureExplorer.tsx — replace qdrant node with pgvector**

Replace:
```typescript
    connections: ['crag', 'qdrant', 'postgres'],
```
With:
```typescript
    connections: ['crag', 'pgvector', 'postgres'],
```

Replace:
```typescript
    id: 'qdrant',
    label: 'Qdrant',
```
With:
```typescript
    id: 'pgvector',
    label: 'pgvector',
```

In `ServiceStatusCard.tsx` line ~67:
```typescript
  qdrant: {
```
Replace this entire `qdrant` key/value with `pgvector`. The service card config at line ~67 is in the `serviceConfig` object — update key from `qdrant` to `pgvector` and label from `"Qdrant"` to `"pgvector"`.

- [ ] **Step 3: PocStatus.tsx — update Qdrant references**

Replace:
```tsx
        description: 'Qdrant vector database con cosine similarity',
```
With:
```tsx
        description: 'pgvector (PostgreSQL extension) con cosine similarity',
```

Replace:
```tsx
        name: 'Qdrant Vector DB',
```
With:
```tsx
        name: 'pgvector',
```

- [ ] **Step 4: TechnicalReference.tsx — update config section**

Replace:
```tsx
    name: 'Qdrant Vector DB',
    container: 'meepleai-qdrant',
```
With:
```tsx
    name: 'pgvector (PostgreSQL)',
    container: 'meepleai-postgres',
```

Replace the block:
```
Configurazione Qdrant:
```
With:
```
Configurazione pgvector:
```

- [ ] **Step 5: rag-data.ts and strategy-details-data.ts — replace Qdrant strings**

In `rag-data.ts`, replace:
```typescript
    dependencies: ['Qdrant', 'PostgreSQL FTS'],
```
With:
```typescript
    dependencies: ['pgvector', 'PostgreSQL FTS'],
```

In `strategy-details-data.ts`, replace:
```typescript
      'Uses Qdrant for vector search, PostgreSQL FTS for keywords',
```
With:
```typescript
      'Uses pgvector for vector search, PostgreSQL FTS for keywords',
```

Replace:
```typescript
      'Qdrant HNSW index for fast ANN search',
```
With:
```typescript
      'pgvector HNSW index for fast ANN search',
```

- [ ] **Step 6: DecisionWalkthrough.tsx, TokenFlowVisualizer.tsx, StrategyFlowVisualizer.tsx — update labels**

In `DecisionWalkthrough.tsx`, replace:
```tsx
          'Vector search: 5 candidates (Qdrant)',
```
With:
```tsx
          'Vector search: 5 candidates (pgvector)',
```

In `TokenFlowVisualizer.tsx`, replace all occurrences:
- `'Cerca queries simili in Qdrant'` → `'Cerca queries simili in pgvector'`
- `'Hybrid search: Vector (Qdrant) + BM25 (keyword)'` → `'Hybrid search: Vector (pgvector) + BM25 (keyword)'`
- `'Similarità semantica in Qdrant'` → `'Similarità semantica in pgvector'`

In `StrategyFlowVisualizer.tsx`, replace:
```tsx
    technical: 'Vector search in Qdrant con cosine similarity.
```
With:
```tsx
    technical: 'Vector search in pgvector con cosine similarity.
```

- [ ] **Step 7: CommandCenterDashboard.tsx — remove qdrant service card**

Remove the line:
```tsx
  { id: 'qdrant', name: 'Qdrant Vector', status: 'online', latency: 15, uptime: 99.95, icon: HardDrive },
```

Also remove the Qdrant ServiceStatusCard section (~line 250):
```tsx
          title="Vector Store (Qdrant)"
```
Update it to:
```tsx
          title="Vector Store (pgvector)"
```
And update the corresponding service reference from `'qdrant'` to `'pgvector'` (or `'postgres'`).

- [ ] **Step 8: knowledge-base/page.tsx and documents/page.tsx and RagTab.tsx — update descriptions**

In `knowledge-base/page.tsx`, replace:
```tsx
      'Manage Qdrant vector collections, view embeddings health, and run similarity searches',
```
With:
```tsx
      'Manage pgvector embeddings, view indexing health, and run similarity searches',
```

Replace:
```tsx
          description="Qdrant collections, point counts, and collection health"
```
With:
```tsx
          description="pgvector stats, game breakdown, and collection health"
```

In `knowledge-base/documents/page.tsx`, replace:
```tsx
              <span className="text-muted-foreground">Qdrant:</span>
              ...storageHealth.qdrant...
```
With:
```tsx
              <span className="text-muted-foreground">Vector Store:</span>
              ...storageHealth.vectorStore...
```
(Follow the same pattern as kb-settings.tsx Step 1.)

In `RagTab.tsx`, replace any `Qdrant` string references with `pgvector`.

- [ ] **Step 9: Verify TypeScript**

```bash
cd apps/web && pnpm typecheck 2>&1 | head -30
```
Expected: 0 errors (or only errors in test files — will fix next task).

- [ ] **Step 10: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/kb-settings.tsx
git add apps/web/src/components/rag-dashboard/ArchitectureExplorer.tsx
git add apps/web/src/components/rag-dashboard/PocStatus.tsx
git add apps/web/src/components/rag-dashboard/TechnicalReference.tsx
git add apps/web/src/components/rag-dashboard/rag-data.ts
git add "apps/web/src/components/rag-dashboard/retrieval-strategies/strategy-details-data.ts"
git add apps/web/src/components/rag-dashboard/DecisionWalkthrough.tsx
git add "apps/web/src/components/rag-dashboard/metrics/TokenFlowVisualizer.tsx"
git add apps/web/src/components/rag-dashboard/StrategyFlowVisualizer.tsx
git add apps/web/src/components/admin/command-center/CommandCenterDashboard.tsx
git add apps/web/src/components/admin/ServiceStatusCard.tsx
git add "apps/web/src/app/admin/(dashboard)/knowledge-base/documents/page.tsx"
git add "apps/web/src/app/admin/(dashboard)/knowledge-base/page.tsx"
git add "apps/web/src/app/admin/(dashboard)/ai/RagTab.tsx"
git commit -m "chore(fe): replace all Qdrant labels with pgvector across dashboard, RAG components, and monitor"
```

---

## Task 9: Infrastructure cleanup

**Files:**
- Delete: `infra/secrets/qdrant.secret`
- Modify: `infra/prometheus/alerts/api-performance.yml`
- Modify: `infra/prometheus/alerts/quality-metrics.yml`
- Modify: `infra/prometheus-rules.yml`
- Modify: `infra/alertmanager.yml`

- [ ] **Step 1: Delete qdrant.secret**

```bash
git rm infra/secrets/qdrant.secret
```

- [ ] **Step 2: Remove Qdrant checks from api-performance.yml**

Open `infra/prometheus/alerts/api-performance.yml`. Find and remove any alert rules that reference `qdrant` (e.g., `qdrant_container`, `qdrant_connection`, `qdrant_health`). Keep all other rules intact.

Run:
```bash
grep -i "qdrant" infra/prometheus/alerts/api-performance.yml
```
Expected: no output.

- [ ] **Step 3: Remove Qdrant checks from quality-metrics.yml**

```bash
grep -i "qdrant" infra/prometheus/alerts/quality-metrics.yml
```
Remove each matching alert rule block. Expected after: no output.

- [ ] **Step 4: Remove Qdrant rules from prometheus-rules.yml**

```bash
grep -n "qdrant" infra/prometheus-rules.yml
```
Remove each matching rule block. Expected after: no output.

- [ ] **Step 5: Update alertmanager.yml comment if needed**

```bash
grep -i "qdrant" infra/alertmanager.yml
```
If present, remove or update. Expected: no output.

- [ ] **Step 6: Commit**

```bash
git add infra/prometheus/alerts/api-performance.yml
git add infra/prometheus/alerts/quality-metrics.yml
git add infra/prometheus-rules.yml
git add infra/alertmanager.yml
git commit -m "chore(infra): delete qdrant.secret and remove Qdrant prometheus rules"
```

---

## Task 10: Frontend tests — update all Qdrant references

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/__tests__/kb-hub-gaps.test.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/services/__tests__/ServicesDashboard.test.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/operations/__tests__/ResourcesTab.test.tsx`
- Modify: `apps/web/src/components/admin/__tests__/ServiceCard.test.tsx`
- Modify: `apps/web/src/components/admin/__tests__/ServiceHealthMatrix.test.tsx`
- Modify: `apps/web/src/components/admin/__tests__/SystemStatus.test.tsx`
- Modify: `apps/web/src/components/rag-dashboard/__tests__/ArchitectureExplorer.test.tsx`

- [ ] **Step 1: Run current tests to establish baseline**

```bash
cd apps/web && pnpm test --run 2>&1 | grep -E "FAIL|PASS|Tests:" | tail -10
```

- [ ] **Step 2: Update kb-hub-gaps.test.tsx**

Replace the mock setup section that has:
```tsx
  searchQdrantCollection: vi.fn().mockResolvedValue({
    ...
  }),
  deleteQdrantCollection: vi.fn(),
  rebuildQdrantIndex: vi.fn(),
```
With:
```tsx
  searchVectors: vi.fn().mockResolvedValue({
    results: [
      {
        documentId: 'doc-1',
        text: 'Sample vector search result text',
        chunkIndex: 0,
        pageNumber: 1,
      },
    ],
    errorMessage: null,
  }),
  getVectorStats: vi.fn().mockResolvedValue({
    totalVectors: 1000,
    dimensions: 768,
    gamesIndexed: 5,
    avgHealthPercent: 95,
    sizeEstimateBytes: 3072000,
    gameBreakdown: [],
  }),
```

Update the test section `// ── Qdrant Search Panel tests ─────────────────────────────────────────────────` (line ~243):
- Change the section header comment from `Qdrant Search Panel` to `Vector Search Panel`
- Update assertions that call `mockAdminClient.searchQdrantCollection` to use `mockAdminClient.searchVectors`
- Remove any tests that click "Select collection" dropdown (it no longer exists)
- Keep tests for search query input, search button, and result display

- [ ] **Step 3: Update ServicesDashboard.test.tsx**

Find the mock service data containing:
```tsx
        serviceName: 'Qdrant',
```
And either remove it entirely, or replace with `pgvector`:
```tsx
        serviceName: 'pgvector',
```

Find assertions:
```tsx
    expect(screen.getByText('Qdrant')).toBeInTheDocument();
```
Replace with:
```tsx
    expect(screen.queryByText('Qdrant')).not.toBeInTheDocument();
```
or update to check for 'pgvector' if that service is now in the dashboard.

Remove the line checking Qdrant uptime percentage (~line 190):
```tsx
    // Qdrant 99.8 shows as 99.8%
    expect(screen.getByText('99.8%')).toBeInTheDocument(); // Qdrant
```

- [ ] **Step 4: Update ResourcesTab.test.tsx**

Find:
```tsx
      expect(screen.getByText('Vector Store (Qdrant)')).toBeInTheDocument();
```
Replace with:
```tsx
      expect(screen.getByText('Vector Store (pgvector)')).toBeInTheDocument();
```
(Or remove if that element is no longer rendered at all.)

- [ ] **Step 5: Update ServiceCard.test.tsx**

All occurrences of `serviceName="qdrant"` should either be:
- Removed (delete the test cases that only test the qdrant service card), or
- Updated to `serviceName="pgvector"` if pgvector is a valid service name

Find:
```tsx
    render(<ServiceCard serviceName="qdrant" status="Unhealthy" locale="it" />);
```
And:
```tsx
    it('displays "Qdrant" for "qdrant" service', () => {
      render(<ServiceCard serviceName="qdrant" status="Healthy" locale="it" />);
      expect(screen.getByText('Qdrant')).toBeInTheDocument();
    });
```

For each qdrant test: check if `ServiceStatusCard.tsx` still has a config entry for the `qdrant` service name. Since we renamed it to `pgvector` in Task 8, the `qdrant` serviceName won't have special config. Either:
1. Remove test cases for `serviceName="qdrant"`, or
2. Replace with `serviceName="pgvector"` tests that assert `screen.getByText('pgvector')`

- [ ] **Step 6: Update ServiceHealthMatrix.test.tsx**

Replace:
```tsx
      createMockService('qdrant', 'Degraded', 1200000),
```
With:
```tsx
      createMockService('pgvector', 'Healthy', 1200000),
```

Replace any `expect(screen.getByText('Qdrant'))` with `expect(screen.queryByText('Qdrant')).not.toBeInTheDocument()`.

- [ ] **Step 7: Update SystemStatus.test.tsx**

Replace:
```tsx
      { name: 'Qdrant', status: 'unhealthy', message: 'Connection failed' },
```
With:
```tsx
      { name: 'pgvector', status: 'healthy', message: '' },
```

Replace:
```tsx
      expect(screen.getByText('Qdrant')).toBeInTheDocument()
```
With:
```tsx
      expect(screen.queryByText('Qdrant')).not.toBeInTheDocument()
```

- [ ] **Step 8: Update ArchitectureExplorer.test.tsx**

Replace:
```tsx
      const qdrantNode = screen.getByText('Qdrant');
      await user.click(qdrantNode);
```
With:
```tsx
      const pgvectorNode = screen.getByText('pgvector');
      await user.click(pgvectorNode);
```

Replace:
```tsx
      expect(screen.getByText('Qdrant')).toBeInTheDocument();
```
With:
```tsx
      expect(screen.getByText('pgvector')).toBeInTheDocument();
```

- [ ] **Step 9: Run all frontend tests**

```bash
cd apps/web && pnpm test --run 2>&1 | tail -20
```
Expected: all tests PASS (0 failures).

- [ ] **Step 10: Commit**

```bash
git add "apps/web/src/app/admin/(dashboard)/knowledge-base/__tests__/kb-hub-gaps.test.tsx"
git add "apps/web/src/app/admin/(dashboard)/monitor/services/__tests__/ServicesDashboard.test.tsx"
git add "apps/web/src/app/admin/(dashboard)/monitor/operations/__tests__/ResourcesTab.test.tsx"
git add apps/web/src/components/admin/__tests__/ServiceCard.test.tsx
git add apps/web/src/components/admin/__tests__/ServiceHealthMatrix.test.tsx
git add apps/web/src/components/admin/__tests__/SystemStatus.test.tsx
git add "apps/web/src/components/rag-dashboard/__tests__/ArchitectureExplorer.test.tsx"
git commit -m "test(fe): remove Qdrant test mocks and assertions, update to pgvector"
```

---

## Task 11: Final validation — full test suite

- [ ] **Step 1: Run backend tests**

```bash
cd apps/api && dotnet test 2>&1 | tail -10
```
Expected: all tests pass (0 failures).

- [ ] **Step 2: Run frontend tests**

```bash
cd apps/web && pnpm test --run 2>&1 | tail -10
```
Expected: all tests pass.

- [ ] **Step 3: TypeScript check**

```bash
cd apps/web && pnpm typecheck 2>&1 | tail -5
```
Expected: 0 errors.

- [ ] **Step 4: Grep for any remaining Qdrant references (excluding ADRs)**

```bash
grep -ri "qdrant" \
  apps/api/src apps/web/src apps/web/__tests__ \
  infra/secrets infra/prometheus \
  --include="*.cs" --include="*.ts" --include="*.tsx" --include="*.yml" \
  --exclude-dir=".next" --exclude-dir="node_modules" \
  2>/dev/null
```
Expected: no output.

- [ ] **Step 5: Create PR**

```bash
git push -u origin HEAD
gh pr create \
  --base main-dev \
  --title "chore(kb): remove all Qdrant code, wire vectors page to pgvector" \
  --body "$(cat <<'EOF'
## Summary

- Removed all Qdrant code, types, and infrastructure (backend + frontend)
- Added `GET /api/v1/admin/kb/vector-stats` and `POST /api/v1/admin/kb/vector-search` endpoints (CQRS)
- Refactored `QdrantInfoDto` → `VectorStoreInfoDto` with real pgvector vector count
- Rewrote `/admin/knowledge-base/vectors` page to show per-game stats and semantic search
- Removed `infra/secrets/qdrant.secret` and all Prometheus Qdrant alert rules
- Updated all RAG dashboard, monitor, and admin components to say pgvector
- Updated all frontend tests (removed Qdrant mocks, updated assertions)

## Test plan

- [ ] `dotnet test` passes with 0 failures
- [ ] `pnpm test --run` passes with 0 failures
- [ ] `pnpm typecheck` passes with 0 errors
- [ ] `grep -ri qdrant apps/api/src apps/web/src` returns empty
- [ ] `/admin/knowledge-base/vectors` loads and shows stats cards
- [ ] Semantic search returns results (when API is running)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## Spec Coverage Check

| Spec Requirement | Task |
|-----------------|------|
| `QdrantInfoDto` → `VectorStoreInfoDto` | Task 1 |
| `storageHealth.Qdrant.*` → `VectorStore.*` in AdminPipelineEndpoints | Task 1 |
| Remove `/health/qdrant` endpoint | Task 2 |
| Remove `qdrant.secret` from AdminSecretsEndpoints | Task 2 |
| `GET /api/v1/admin/kb/vector-stats` | Task 3 |
| `GetVectorStatsQuery` + Handler | Task 3 |
| `POST /api/v1/admin/kb/vector-search` | Task 4 |
| `VectorSemanticSearchQuery` + Handler | Task 4 |
| Frontend schema update (QdrantInfo → VectorStoreInfo) | Task 5 |
| Remove Qdrant methods from adminAiClient | Task 5 |
| Add `getVectorStats`, `searchVectors` | Task 5 |
| New `vector-game-card.tsx` | Task 6 |
| Delete `vector-collection-card.tsx` | Task 6 |
| Rewrite `vectors/page.tsx` for pgvector | Task 7 |
| `kb-settings.tsx` remove Rebuild Qdrant | Task 8 |
| RAG dashboard Qdrant → pgvector labels | Task 8 |
| Monitor remove qdrant service card | Task 8 |
| `knowledge-base/page.tsx` description | Task 8 |
| `knowledge-base/documents/page.tsx` storageHealth | Task 8 |
| `RagTab.tsx` description | Task 8 |
| Delete `infra/secrets/qdrant.secret` | Task 9 |
| Prometheus alerts remove Qdrant rules | Task 9 |
| `kb-hub-gaps.test.tsx` update mocks | Task 10 |
| Monitor tests remove Qdrant assertions | Task 10 |
| `ArchitectureExplorer.test.tsx` update node | Task 10 |
| Backend unit tests for new handlers | Tasks 3 + 4 |
