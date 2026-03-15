# RAG Pipeline Integration Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the RAG enhancement services (Adaptive, CRAG, RAG-Fusion, RAPTOR) into the live pipeline so they have runtime effect, plus add admin toggle endpoints.

**Architecture:** Modify `RagPromptAssemblyService` to check active enhancements via `IRagEnhancementService` and invoke the appropriate service at each pipeline stage. Add RAPTOR hook in `PdfProcessingPipelineService`. Add admin REST endpoints following the `RagPipelineAdminEndpoints` pattern.

**Tech Stack:** .NET 9 / C# / MediatR / EF Core / FluentValidation / xUnit

---

## Chunk 1: Integrate Adaptive RAG + CRAG + RAG-Fusion into RagPromptAssemblyService

### Task 1: Modify RagPromptAssemblyService

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/RagPromptAssemblyEnhancementsTests.cs`

**What to change:**

1. Add constructor dependencies: `IRagEnhancementService`, `IQueryComplexityClassifier`, `IRetrievalRelevanceEvaluator`, `IQueryExpander`
2. Add `UserTier` parameter to `RetrieveRagContextAsync` (or pass it through the existing request DTO)
3. In `RetrieveRagContextAsync`, BEFORE the hybrid search call:
   - Check if AdaptiveRouting is active
   - If active, classify query complexity
   - If Simple: skip retrieval entirely, return empty context with a flag
4. In `RetrieveRagContextAsync`, AFTER hybrid search + reranking but BEFORE building context:
   - Check if CragEvaluation is active
   - If active, evaluate chunk relevance
   - If Incorrect: re-run search with expanded query
   - If Ambiguous: merge original + expanded results
5. In `RetrieveRagContextAsync`, BEFORE the hybrid search call (after adaptive check):
   - Check if RagFusionQueries is active
   - If active, expand query into 3-4 variants via IQueryExpander
   - Run parallel hybrid searches for each variant
   - Merge results using existing RRF logic

**Integration pattern:**
```csharp
// In RetrieveRagContextAsync, at the beginning:
var activeEnhancements = await _ragEnhancementService
    .GetActiveEnhancementsAsync(userTier, ct);

// ADAPTIVE RAG: Skip retrieval for simple queries
if (activeEnhancements.HasFlag(RagEnhancement.AdaptiveRouting))
{
    var complexity = await _complexityClassifier.ClassifyAsync(question, ct);
    if (!complexity.RequiresRetrieval)
    {
        _logger.LogInformation("Adaptive RAG: skipping retrieval for simple query");
        return (new List<ChunkCitation>(), 0f); // empty context
    }
}

// RAG-FUSION: Expand query into variants
var queries = new List<string> { question };
if (activeEnhancements.HasFlag(RagEnhancement.RagFusionQueries))
{
    queries = await _queryExpander.ExpandAsync(question, ct);
}

// Execute search for each query variant
var allResults = new List<TextChunkMatch>();
foreach (var q in queries)
{
    var results = await TryHybridSearchAsync(q, gameId, ...);
    allResults.AddRange(results);
}
// Deduplicate by chunk ID, keep highest score
var deduplicated = allResults
    .GroupBy(r => (r.PdfDocumentId, r.ChunkIndex))
    .Select(g => g.OrderByDescending(r => r.Rank).First())
    .ToList();

// Rerank deduplicated results
var reranked = await TryRerankAsync(question, deduplicated, ct);

// CRAG: Evaluate relevance before using chunks
if (activeEnhancements.HasFlag(RagEnhancement.CragEvaluation))
{
    var scored = reranked.Select(c => new ScoredChunk(
        c.PdfDocumentId.ToString(), c.Content, c.Rank)).ToList();
    var evaluation = await _relevanceEvaluator.EvaluateAsync(question, scored, ct);

    if (evaluation.ShouldRequery)
    {
        _logger.LogInformation("CRAG: {Verdict}, expanding retrieval", evaluation.Verdict);
        var expanded = await TryHybridSearchAsync(
            await ExpandQueryAsync(question, ct) ?? question, gameId, ...);
        if (evaluation.UseRetrievedDocuments)
            reranked = MergeAndDedup(reranked, expanded);
        else
            reranked = expanded;
    }
}
```

- [ ] **Step 1: Write integration test for adaptive routing skip**
- [ ] **Step 2: Write integration test for CRAG re-query**
- [ ] **Step 3: Write integration test for RAG-Fusion multi-query**
- [ ] **Step 4: Add new constructor dependencies to RagPromptAssemblyService**
- [ ] **Step 5: Implement adaptive routing check before retrieval**
- [ ] **Step 6: Implement RAG-Fusion query expansion before search**
- [ ] **Step 7: Implement CRAG evaluation after reranking**
- [ ] **Step 8: Update DI registration if needed**
- [ ] **Step 9: Run all tests**
- [ ] **Step 10: Commit**

```bash
git commit -m "feat(rag): integrate adaptive routing, CRAG, and RAG-Fusion into retrieval pipeline"
```

---

## Chunk 2: Hook RAPTOR into PdfProcessingPipelineService

### Task 2: Add RAPTOR indexing step to document processing

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/DependencyInjection/` (add IRaptorIndexer dependency)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/RaptorPipelineIntegrationTests.cs`

**What to change:**

After chunking (line ~105) and before embedding, add optional RAPTOR tree building:

```csharp
// After chunks are created, before embedding
// RAPTOR: Build hierarchical summary tree if enhancement is enabled
if (chunks.Count > 0)
{
    try
    {
        var raptorSummaries = await _raptorIndexer.BuildTreeAsync(
            pdfDocument.Id, pdfDocument.GameId,
            chunks.Select(c => c.Content).ToList(),
            maxLevels: 3, ct);

        if (raptorSummaries.TotalNodes > 0)
        {
            // Save summaries to DB
            await SaveRaptorSummariesAsync(pdfDocument.Id, pdfDocument.GameId,
                raptorSummaries.Summaries, ct);
            _logger.LogInformation("RAPTOR: built {Levels} levels with {Nodes} nodes",
                raptorSummaries.Levels, raptorSummaries.TotalNodes);
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "RAPTOR indexing failed, continuing without hierarchical summaries");
        // Non-blocking: document processing continues even if RAPTOR fails
    }
}
```

- [ ] **Step 1: Write test for RAPTOR hook in pipeline**
- [ ] **Step 2: Add IRaptorIndexer + IRagEnhancementService to PdfProcessingPipelineService constructor**
- [ ] **Step 3: Implement RAPTOR hook after chunking**
- [ ] **Step 4: Add SaveRaptorSummariesAsync helper method**
- [ ] **Step 5: Run tests, commit**

```bash
git commit -m "feat(rag): hook RAPTOR hierarchical indexing into PDF processing pipeline"
```

---

## Chunk 3: Admin Endpoints for RAG Enhancement Toggles

### Task 3: Add admin endpoints

**Files:**
- Create: `apps/api/src/Api/Routing/Admin/RagEnhancementAdminEndpoints.cs`
- Modify: `apps/api/src/Api/Routing/` (register new endpoint group in main routing)
- Test: N/A (endpoints tested via integration tests)

**Endpoints:**

```
GET  /api/v1/admin/rag-enhancements          → List all enhancements with status
POST /api/v1/admin/rag-enhancements/{key}/toggle  → Toggle enhancement globally
POST /api/v1/admin/rag-enhancements/{key}/tier/{tier}/toggle  → Toggle per tier
GET  /api/v1/rag/enhancements/estimate        → User-facing: active enhancements + extra credits
```

- [ ] **Step 1: Create RagEnhancementAdminEndpoints.cs**
- [ ] **Step 2: Register in main routing**
- [ ] **Step 3: Add user-facing estimate endpoint**
- [ ] **Step 4: Run build, verify endpoints appear in Swagger**
- [ ] **Step 5: Commit**

```bash
git commit -m "feat(rag): add admin endpoints for RAG enhancement toggles and cost estimates"
```

---

## Chunk 4: Migration fix for RaptorSummaries FK

### Task 4: Add missing GameId FK

- [ ] **Step 1: Create migration to add FK on RaptorSummaries.GameId + composite index**
- [ ] **Step 2: Commit**

```bash
git commit -m "fix(rag): add missing FK constraint and composite index on RaptorSummaries"
```
