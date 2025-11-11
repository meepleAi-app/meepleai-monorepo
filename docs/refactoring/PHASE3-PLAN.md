# Phase 3: KnowledgeBase Context - Split RagService

**Status**: READY TO START
**Current**: RagService.cs (995 lines) - Too complex
**Target**: 5 focused domain services (~200 lines each)

---

## Current RagService Analysis

**File**: `Services/RagService.cs` (995 lines)

**Responsibilities** (multiple concerns - violates SRP):
1. **Query Processing**: Query expansion, embedding generation
2. **Vector Search**: Qdrant search, result reranking
3. **Hybrid Search**: Combining vector + keyword search with RRF
4. **LLM Orchestration**: Prompt building, LLM calls, streaming
5. **Quality Tracking**: Confidence scoring, quality metrics
6. **Caching**: Response caching, cache key generation
7. **Configuration**: Dynamic config retrieval (TopK, MinScore, RRF-K)

**Already Extracted Services** (in `Services/Rag/` folder):
- ✅ QueryExpansionService (~200 lines)
- ✅ SearchResultReranker (~150 lines)
- ✅ CitationExtractorService (~180 lines)

**Main Methods** (4):
1. `AskAsync()` - Standard RAG Q&A (212 lines)
2. `ExplainAsync()` - Structured explanations (161 lines)
3. `AskWithHybridSearchAsync()` - Hybrid search (204 lines)
4. `AskWithCustomPromptAsync()` - Custom prompt evaluation (191 lines)

**Dependencies** (11):
- DbContext, EmbeddingService, QdrantService, HybridSearchService
- LlmService, Cache, PromptTemplateService, Logger
- ConfigurationService, Configuration
- QueryExpansion, Reranker, CitationExtractor (already extracted)

---

## Phase 3 Strategy: Move to KnowledgeBase Bounded Context

### Option A: Full DDD Refactoring (2-3 days)
**Create complete bounded context**:
1. Domain entities: VectorDocument, Embedding, SearchResult
2. Value objects: Vector, Confidence, Citation
3. Domain services: Split RagService logic
4. Application: CQRS commands/queries
5. Infrastructure: Repositories

**Pros**: Pure DDD, clean architecture
**Cons**: 2-3 days, complex domain modeling

### Option B: Pragmatic Service Split (0.5-1 day) ⭐ RECOMMENDED
**Move and organize existing code**:
1. Create `BoundedContexts/KnowledgeBase/Services/` folder
2. Move existing services there:
   - QueryExpansionService
   - SearchResultReranker
   - CitationExtractorService
3. Split RagService into focused services:
   - VectorSearchService (Qdrant operations)
   - HybridSearchCoordinator (RRF fusion)
   - LlmOrchestrationService (Prompt + LLM calls)
   - QualityTrackingService (Confidence metrics)
4. Keep existing architecture (no entities/value objects yet)
5. Move to DDD later if needed

**Pros**: Fast, achieves complexity reduction goal
**Cons**: Not "pure DDD" (but well-organized)

---

## Recommended Approach for Alpha: Option B

Given:
- We've already spent ~10 hours on Phase 1-2
- Phase 2 proved entity migration is complex
- Original goal was "organize code, reduce complexity"
- Alpha needs functional value fast

**Option B achieves**:
- ✅ RagService split from 995 → 4-5 services (~200 lines each)
- ✅ Services organized in KnowledgeBase folder
- ✅ Clear separation of concerns
- ✅ Easier to navigate and maintain
- ✅ Can add DDD patterns later if needed

---

## Detailed Plan: Option B (Service Split)

### Step 1: Create KnowledgeBase Folder Structure (30 min)
```
BoundedContexts/KnowledgeBase/
├── Services/
│   ├── VectorSearchService.cs        (~200 lines)
│   ├── HybridSearchCoordinator.cs    (~250 lines)
│   ├── LlmOrchestrationService.cs    (~300 lines)
│   ├── QualityTrackingService.cs     (~150 lines)
│   ├── QueryExpansionService.cs      (move existing)
│   ├── SearchResultReranker.cs       (move existing)
│   └── CitationExtractorService.cs   (move existing)
├── Models/
│   ├── SearchRequest.cs
│   ├── SearchResult.cs
│   └── QaResponse.cs (move from Api.Models)
└── DependencyInjection/
    └── KnowledgeBaseServiceExtensions.cs
```

### Step 2: Extract VectorSearchService (1 hour)
**Responsibilities**:
- Qdrant vector search operations
- Embedding generation for queries
- Result scoring and filtering

**Methods** (extract from RagService):
- `SearchVectorAsync(gameId, query, topK, minScore)`
- `GenerateQueryEmbeddingAsync(query, language)`
- `FilterByScoreAsync(results, minScore)`

### Step 3: Extract HybridSearchCoordinator (1 hour)
**Responsibilities**:
- Coordinate vector + keyword search
- RRF (Reciprocal Rank Fusion) combining
- Search mode switching (Semantic/Keyword/Hybrid)

**Methods**:
- `SearchAsync(gameId, query, searchMode)`
- `FuseResultsAsync(vectorResults, keywordResults, rrfK)`
- `RankResultsAsync(results)`

### Step 4: Extract LlmOrchestrationService (1.5 hours)
**Responsibilities**:
- Prompt template retrieval and building
- LLM API calls
- Response parsing and formatting

**Methods**:
- `GenerateAnswerAsync(query, context, language)`
- `BuildPromptAsync(query, snippets, language)`
- `CallLlmAsync(prompt, model)`
- `ParseResponseAsync(llmResponse)`

### Step 5: Extract QualityTrackingService (1 hour)
**Responsibilities**:
- Confidence score calculation
- Quality metrics tracking
- Low-quality detection

**Methods**:
- `CalculateConfidenceAsync(searchResults, llmResponse)`
- `TrackQualityMetricsAsync(response)`
- `IsLowQualityAsync(confidence)`

### Step 6: Refactor RagService to Coordinator (1 hour)
**New role**: Orchestrate the 4 services
- Inject all 4 services
- Delegate to appropriate service
- Maintain public API (IRagService interface)
- ~200 lines (orchestration only)

**Methods** (orchestration):
```csharp
public async Task<QaResponse> AskAsync(...)
{
    // 1. Vector search
    var vectorResults = await _vectorSearchService.SearchAsync(gameId, query);

    // 2. Generate answer
    var answer = await _llmOrchestrationService.GenerateAnswerAsync(query, vectorResults);

    // 3. Track quality
    await _qualityTrackingService.TrackAsync(answer);

    return answer;
}
```

---

## Timeline: Option B

| Step | Duration | Output |
|------|----------|--------|
| 1. Create structure | 30 min | Folders + DI setup |
| 2. VectorSearchService | 1 hour | ~200 line service |
| 3. HybridSearchCoordinator | 1 hour | ~250 line service |
| 4. LlmOrchestrationService | 1.5 hours | ~300 line service |
| 5. QualityTrackingService | 1 hour | ~150 line service |
| 6. Refactor RagService | 1 hour | ~200 line coordinator |
| **Total** | **6 hours** | **5 services + coordinator** |

**Result**:
- ✅ RagService: 995 lines → 200 lines (coordinator)
- ✅ 4 new focused services (~200-300 lines each)
- ✅ Clear separation of concerns
- ✅ Easier to test and maintain

---

## Next Steps

1. **Decision**: Confirm Option B (pragmatic split) vs Option A (full DDD)
2. **Start**: Create KnowledgeBase folder structure
3. **Extract**: Create 4 new services systematically
4. **Refactor**: Slim down RagService to coordinator
5. **Test**: Add focused tests for each service
6. **Commit**: Document Phase 3 completion

**Estimated Completion**: Same day (6 hours) or next session

Ready to start?
