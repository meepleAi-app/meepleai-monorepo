# Wire pgvector into PDF Pipeline & Hybrid Search

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Connect the existing PgVectorStoreAdapter to the PDF processing pipeline (indexing) and hybrid search (retrieval), so embeddings generated during PDF processing are stored in pgvector and used for semantic search.

**Architecture:** The PDF pipeline already generates embeddings but discards them (IndexInQdrantAsync is a NO-OP). PgVectorStoreAdapter already has working code for HNSW-indexed cosine search + bulk insert. HybridSearchService already has RRF fusion but passes empty vector results. We wire these 3 pieces together. No new services needed — just inject existing interfaces and call existing methods.

**Tech Stack:** .NET 9, EF Core, pgvector (PostgreSQL extension), Npgsql, sentence-transformers (existing embedding service)

---

## File Map

| Action | File | Responsibility |
|--------|------|---------------|
| **Modify** | `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs` | Wire IndexInQdrantAsync to call PgVectorStoreAdapter.IndexBatchAsync |
| **Modify** | `apps/api/src/Api/Services/HybridSearchService.cs` | Add IEmbeddingService + IQdrantVectorStoreAdapter deps, generate query embedding, call pgvector search |
| **Modify** | `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/DependencyInjection/DocumentProcessingServiceExtensions.cs` | Register IQdrantVectorStoreAdapter in DocumentProcessing DI |
| **Create** | `apps/api/src/Api/Migrations/YYYYMMDDHHMMSS_AddPgVectorEmbeddingsTable.cs` | EF migration for pgvector_embeddings table + HNSW index |
| **Modify** | `tests/Api.Tests/` (new test files) | Integration tests for indexing + search |

---

## Task 1: Create pgvector_embeddings table via migration

**Files:**
- Create: `apps/api/src/Api/` (EF migration auto-generated)

- [ ] **Step 1: Create the migration SQL script**

We need a raw SQL migration since pgvector types aren't natively supported by EF Core's model builder. Create a migration class:

```bash
cd apps/api/src/Api
dotnet ef migrations add AddPgVectorEmbeddingsTable
```

Then replace the generated migration body with:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Enable pgvector extension
    migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

    // Create pgvector_embeddings table
    migrationBuilder.Sql(@"
        CREATE TABLE IF NOT EXISTS pgvector_embeddings (
            id UUID PRIMARY KEY,
            vector_document_id UUID NOT NULL,
            game_id UUID NOT NULL,
            text_content TEXT NOT NULL,
            vector vector(1024) NOT NULL,
            model TEXT NOT NULL,
            chunk_index INTEGER NOT NULL,
            page_number INTEGER NOT NULL,
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );
    ");

    // HNSW index for cosine similarity search
    migrationBuilder.Sql(@"
        CREATE INDEX IF NOT EXISTS idx_pgvector_embeddings_vector_cosine
        ON pgvector_embeddings
        USING hnsw (vector vector_cosine_ops)
        WITH (m = 16, ef_construction = 200);
    ");

    // Filtering indexes
    migrationBuilder.Sql(@"
        CREATE INDEX IF NOT EXISTS idx_pgvector_embeddings_game_id
        ON pgvector_embeddings (game_id);
    ");

    migrationBuilder.Sql(@"
        CREATE INDEX IF NOT EXISTS idx_pgvector_embeddings_vector_document_id
        ON pgvector_embeddings (vector_document_id);
    ");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql("DROP TABLE IF EXISTS pgvector_embeddings;");
}
```

- [ ] **Step 2: Apply migration locally**

```bash
cd apps/api/src/Api
dotnet ef database update
```

Expected: Migration applies cleanly. Table `pgvector_embeddings` created with HNSW index.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Migrations/
git commit -m "feat(db): add pgvector_embeddings table with HNSW index for semantic search"
```

---

## Task 2: Wire pipeline to store embeddings in pgvector

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/DependencyInjection/DocumentProcessingServiceExtensions.cs`

- [ ] **Step 1: Add IQdrantVectorStoreAdapter dependency to PdfProcessingPipelineService**

In `PdfProcessingPipelineService.cs`, add the new dependency:

```csharp
// Add to fields (after _raptorIndexer):
private readonly IQdrantVectorStoreAdapter? _vectorStore;

// Add to constructor parameters (after IRaptorIndexer? raptorIndexer = null):
IQdrantVectorStoreAdapter? vectorStore = null

// Add to constructor body:
_vectorStore = vectorStore;
```

Add the using:
```csharp
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
```

- [ ] **Step 2: Implement IndexInQdrantAsync to call pgvector**

Replace the NO-OP `IndexInQdrantAsync` method (lines 364-403) with:

```csharp
private async Task IndexInQdrantAsync(
    PdfDocumentEntity pdfDoc,
    List<DocumentChunkInput> chunks,
    List<float[]> embeddings,
    CancellationToken cancellationToken)
{
    var chunkCount = chunks.Count;

    // Update or create VectorDocument record (existing tracking logic)
    var vectorDoc = await _db.VectorDocuments
        .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfDoc.Id, cancellationToken)
        .ConfigureAwait(false);

    if (vectorDoc == null)
    {
        vectorDoc = new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = pdfDoc.GameId,
            SharedGameId = pdfDoc.SharedGameId,
            PdfDocumentId = pdfDoc.Id,
            IndexingStatus = "completed",
            ChunkCount = chunkCount,
            TotalCharacters = pdfDoc.ExtractedText?.Length ?? 0,
            IndexedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        _db.VectorDocuments.Add(vectorDoc);
    }
    else
    {
        vectorDoc.IndexingStatus = "completed";
        vectorDoc.ChunkCount = chunkCount;
        vectorDoc.TotalCharacters = pdfDoc.ExtractedText?.Length ?? 0;
        vectorDoc.IndexedAt = _timeProvider.GetUtcNow().UtcDateTime;
    }

    await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    // Index embeddings in pgvector (new logic)
    if (_vectorStore != null && embeddings.Count == chunks.Count)
    {
        var gameId = pdfDoc.GameId ?? pdfDoc.SharedGameId ?? Guid.Empty;
        if (gameId == Guid.Empty)
        {
            _logger.LogWarning(
                "[PdfPipeline] No GameId for PDF {PdfId}, skipping pgvector indexing",
                pdfDoc.Id);
            return;
        }

        // Ensure pgvector table + HNSW index exist
        var dimension = embeddings[0].Length;
        await _vectorStore.EnsureCollectionExistsAsync(gameId, dimension, cancellationToken)
            .ConfigureAwait(false);

        // Delete old embeddings for this document (re-processing support)
        await _vectorStore.DeleteByVectorDocumentIdAsync(vectorDoc.Id, cancellationToken)
            .ConfigureAwait(false);

        // Build Embedding domain objects
        var modelName = _embeddingService.GetModelName();
        var embeddingEntities = chunks.Select((chunk, i) =>
            new KnowledgeBase.Domain.Entities.Embedding(
                id: Guid.NewGuid(),
                vectorDocumentId: vectorDoc.Id,
                textContent: chunk.Text,
                vector: new KnowledgeBase.Domain.ValueObjects.Vector(embeddings[i]),
                model: modelName,
                chunkIndex: i,
                pageNumber: Math.Max(1, chunk.Page)))
            .ToList();

        await _vectorStore.IndexBatchAsync(embeddingEntities, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "[PdfPipeline] Indexed {Count} embeddings in pgvector for PDF {PdfId} (gameId={GameId})",
            embeddingEntities.Count, pdfDoc.Id, gameId);
    }
}
```

Add usings at top of file:
```csharp
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using KnowledgeBase = Api.BoundedContexts.KnowledgeBase;
```

- [ ] **Step 3: Register IQdrantVectorStoreAdapter in DocumentProcessing DI**

In `DocumentProcessingServiceExtensions.cs`, the `IQdrantVectorStoreAdapter` is already registered in `KnowledgeBaseServiceExtensions.cs` (line 305). The pipeline service resolves it via constructor injection. No additional DI registration needed — just verify it's scoped (it is: `AddScoped<IQdrantVectorStoreAdapter, PgVectorStoreAdapter>()`).

- [ ] **Step 4: Build and verify compilation**

```bash
cd apps/api/src/Api
dotnet build
```

Expected: Build succeeds with zero errors.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs
git commit -m "feat(pipeline): wire pgvector indexing into PDF processing pipeline"
```

---

## Task 3: Wire hybrid search to use pgvector for semantic search

**Files:**
- Modify: `apps/api/src/Api/Services/HybridSearchService.cs`

- [ ] **Step 1: Add IEmbeddingService and IQdrantVectorStoreAdapter dependencies**

```csharp
// Add to fields:
private readonly IEmbeddingService _embeddingService;
private readonly IQdrantVectorStoreAdapter _vectorStore;

// Update constructor:
public HybridSearchService(
    IKeywordSearchService keywordSearchService,
    IEmbeddingService embeddingService,
    IQdrantVectorStoreAdapter vectorStore,
    ILogger<HybridSearchService> logger,
    IOptions<HybridSearchConfiguration> config)
{
    _keywordSearchService = keywordSearchService;
    _embeddingService = embeddingService;
    _vectorStore = vectorStore;
    _logger = logger;
    _config = config.Value;
}
```

Add usings:
```csharp
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
```

- [ ] **Step 2: Implement SearchSemanticOnlyAsync**

Replace the empty-returning method (lines 94-107) with:

```csharp
private async Task<List<HybridSearchResult>> SearchSemanticOnlyAsync(
    string query,
    Guid gameId,
    int limit,
    List<Guid>? documentIds,
    CancellationToken cancellationToken)
{
    var vectorResults = await ExecuteVectorSearchAsync(
        query, gameId, limit, documentIds, cancellationToken).ConfigureAwait(false);

    return vectorResults.Select((r, index) => new HybridSearchResult
    {
        ChunkId = $"{r.VectorDocumentId}_{r.ChunkIndex}",
        Content = r.TextContent,
        PdfDocumentId = r.VectorDocumentId.ToString(),
        GameId = gameId,
        ChunkIndex = r.ChunkIndex,
        PageNumber = r.PageNumber,
        HybridScore = (float)r.CalculateSimilarity(r), // placeholder — use position
        VectorScore = 1.0f / (index + 1), // normalized rank score
        KeywordScore = null,
        VectorRank = index + 1,
        KeywordRank = null,
        MatchedTerms = new List<string>(),
        Mode = SearchMode.Semantic
    }).ToList();
}
```

- [ ] **Step 3: Implement SearchHybridAsync with real vector results**

Replace the keyword-only hybrid search (lines 157-206) with:

```csharp
private async Task<List<HybridSearchResult>> SearchHybridAsync(
    string query,
    Guid gameId,
    int limit,
    float vectorWeight,
    float keywordWeight,
    List<Guid>? documentIds,
    CancellationToken cancellationToken)
{
    var fetchLimit = Math.Max(limit * 2, 20);

    // Run vector and keyword searches in parallel
    var vectorTask = ExecuteVectorSearchAsync(
        query, gameId, fetchLimit, documentIds, cancellationToken);

    var keywordTask = _keywordSearchService.SearchAsync(
        query,
        gameId,
        fetchLimit,
        phraseSearch: query.Contains('"'),
        boostTerms: _config.BoostTerms,
        cancellationToken: cancellationToken);

    await Task.WhenAll(vectorTask, keywordTask).ConfigureAwait(false);

    var vectorEmbeddings = await vectorTask.ConfigureAwait(false);
    var keywordResults = await keywordTask.ConfigureAwait(false);

    // Apply document filter to keyword results
    var filteredKeywordResults = documentIds == null
        ? keywordResults
        : keywordResults.Where(r => documentIds.Any(id =>
            string.Equals(id.ToString(), r.PdfDocumentId, StringComparison.Ordinal))).ToList();

    // Convert vector results to SearchResultItem for RRF fusion
    var vectorItems = vectorEmbeddings.Select(e => new SearchResultItem
    {
        Score = 1.0f, // cosine similarity already filtered by minScore
        Text = e.TextContent,
        PdfId = e.VectorDocumentId.ToString(),
        ChunkIndex = e.ChunkIndex,
        Page = e.PageNumber
    }).ToArray();

    _logger.LogInformation(
        "Hybrid search: vectorCount={VectorCount}, keywordCount={KeywordCount} (post-filter: {FilteredKeyword})",
        vectorItems.Length, keywordResults.Count, filteredKeywordResults.Count);

    // RRF fusion with both vector AND keyword results
    var fusedResults = FuseSearchResults(
        vectorItems,
        filteredKeywordResults,
        gameId,
        vectorWeight,
        keywordWeight,
        _config.RrfConstant ?? DefaultRrfK);

    var topResults = fusedResults
        .OrderByDescending(r => r.HybridScore)
        .Take(limit)
        .ToList();

    _logger.LogInformation(
        "Hybrid search completed: returned {ResultCount} fused results (from {TotalFused} total)",
        topResults.Count, fusedResults.Count);

    return topResults;
}
```

- [ ] **Step 4: Add helper method ExecuteVectorSearchAsync**

Add this private method to HybridSearchService:

```csharp
/// <summary>
/// Generates query embedding and performs pgvector cosine similarity search.
/// Falls back to empty results if embedding generation fails.
/// </summary>
private async Task<List<KnowledgeBase.Domain.Entities.Embedding>> ExecuteVectorSearchAsync(
    string query,
    Guid gameId,
    int limit,
    List<Guid>? documentIds,
    CancellationToken cancellationToken)
{
    try
    {
        // Generate embedding for the query text
        var embeddingResult = await _embeddingService
            .GenerateEmbeddingAsync(query, cancellationToken)
            .ConfigureAwait(false);

        if (!embeddingResult.Success || embeddingResult.Embeddings is not { Count: > 0 })
        {
            _logger.LogWarning(
                "Query embedding generation failed: {Error}. Falling back to keyword-only.",
                embeddingResult.ErrorMessage);
            return new List<KnowledgeBase.Domain.Entities.Embedding>();
        }

        var queryVector = new Vector(embeddingResult.Embeddings[0]);

        var results = await _vectorStore.SearchAsync(
            gameId,
            queryVector,
            topK: limit,
            minScore: 0.3, // Minimum cosine similarity threshold
            documentIds: documentIds,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "pgvector search returned {Count} results for gameId={GameId}",
            results.Count, gameId);

        return results;
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex,
            "Vector search failed, falling back to keyword-only for gameId={GameId}",
            gameId);
        return new List<KnowledgeBase.Domain.Entities.Embedding>();
    }
}
```

Add usings:
```csharp
using KnowledgeBase = Api.BoundedContexts.KnowledgeBase;
```

- [ ] **Step 5: Build and verify**

```bash
cd apps/api/src/Api
dotnet build
```

Expected: Build succeeds.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Services/HybridSearchService.cs
git commit -m "feat(search): wire pgvector semantic search into hybrid search with RRF fusion"
```

---

## Task 4: Integration test — indexing pipeline

**Files:**
- Create: `tests/Api.Tests/BoundedContexts/DocumentProcessing/Integration/PgVectorIndexingTests.cs`

- [ ] **Step 1: Write integration test for pgvector indexing**

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task IndexInQdrantAsync_WithPgVectorAdapter_StoresEmbeddings()
{
    // Arrange: Create a PDF document, chunks, and embeddings
    // Act: Process through pipeline
    // Assert: pgvector_embeddings table has rows with correct game_id
}
```

This test should use `WebApplicationFactory<Program>` and Testcontainers (PostgreSQL with pgvector extension). Follow existing test patterns in the project.

- [ ] **Step 2: Run test**

```bash
cd apps/api/src/Api
dotnet test --filter "PgVectorIndexingTests"
```

- [ ] **Step 3: Commit**

```bash
git add tests/Api.Tests/BoundedContexts/DocumentProcessing/Integration/PgVectorIndexingTests.cs
git commit -m "test(pipeline): add integration test for pgvector embedding indexing"
```

---

## Task 5: Integration test — hybrid search with pgvector

**Files:**
- Create: `tests/Api.Tests/Services/HybridSearchWithPgVectorTests.cs`

- [ ] **Step 1: Write integration test for hybrid search**

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task SearchAsync_HybridMode_CombinesVectorAndKeywordResults()
{
    // Arrange: Seed pgvector_embeddings + text_chunks with known data
    // Act: Search with a query that matches both keyword and semantically
    // Assert: Results contain both vector-matched and keyword-matched chunks
    //         HybridScore > 0, VectorScore != null, KeywordScore != null
}

[Fact]
[Trait("Category", "Integration")]
public async Task SearchAsync_SemanticMode_ReturnsPgVectorResults()
{
    // Arrange: Seed pgvector_embeddings with known embeddings
    // Act: Search in Semantic mode
    // Assert: Results are non-empty, come from pgvector
}

[Fact]
[Trait("Category", "Integration")]
public async Task SearchAsync_VectorSearchFails_FallsBackToKeywordOnly()
{
    // Arrange: No embeddings in pgvector, but text_chunks has data
    // Act: Search in Hybrid mode
    // Assert: Results come from keyword search only (graceful degradation)
}
```

- [ ] **Step 2: Run tests**

```bash
cd apps/api/src/Api
dotnet test --filter "HybridSearchWithPgVectorTests"
```

- [ ] **Step 3: Commit**

```bash
git add tests/Api.Tests/Services/HybridSearchWithPgVectorTests.cs
git commit -m "test(search): add integration tests for hybrid search with pgvector"
```

---

## Task 6: Full build verification + final commit

- [ ] **Step 1: Run full build**

```bash
cd apps/api/src/Api
dotnet build
```

- [ ] **Step 2: Run all DocumentProcessing + KnowledgeBase tests**

```bash
dotnet test --filter "BoundedContext=DocumentProcessing|BoundedContext=KnowledgeBase" --no-build
```

- [ ] **Step 3: Verify no regressions in existing unit tests**

```bash
dotnet test --filter "Category=Unit" --no-build
```

- [ ] **Step 4: Final commit if any fixes needed**

```bash
git add -A
git commit -m "fix: address test feedback from pgvector wiring"
```

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| **Optional `_vectorStore` in pipeline** | Null-safe: if DI doesn't resolve it, pipeline still works (keyword-only) |
| **Graceful degradation in search** | If embedding generation fails, falls back to keyword-only (existing behavior) |
| **minScore = 0.3 for vector search** | Standard threshold for cosine similarity — filters irrelevant results without being too aggressive |
| **Parallel vector + keyword in hybrid** | Both are I/O-bound (DB queries), parallel execution cuts latency |
| **Re-processing support** | `DeleteByVectorDocumentIdAsync` before re-indexing prevents duplicates |
| **HNSW m=16, ef_construction=200** | Standard params for high-recall approximate search. Good for <100K vectors. |

## Rollback Plan

If pgvector causes issues:
1. Set `_vectorStore = null` in pipeline constructor → embeddings stop being indexed
2. In `HybridSearchService.ExecuteVectorSearchAsync`, the try/catch already falls back to keyword-only
3. No data loss — text_chunks table still has all data for keyword search
