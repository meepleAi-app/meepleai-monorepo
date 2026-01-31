# ADR-016: Advanced PDF Embedding Pipeline with Hybrid Indexing

**Status**: Accepted
**Date**: 2025-12-03
**Deciders**: Engineering Lead, ML Engineer
**Context**: Issue #1901 - Enhanced RAG Pipeline
**Related**: ADR-001 (Hybrid RAG), ADR-003b (Unstructured PDF)

---

## Context

MeepleAI's current RAG pipeline achieves baseline accuracy but requires enhancement for complex board game rulebooks with:
- Tables (game setup, component counts, scoring)
- Multi-column layouts (quick reference cards)
- Hierarchical structure (phases, rounds, turns)
- Cross-references ("see Combat section on page 12")

**Current State** (from ADR-001, ADR-003b):
- PDF extraction: 3-stage fallback (Unstructured → SmolDocling → Docnet)
- Chunking: Basic sentence-based (TextChunkingService)
- Embedding: OpenRouter text-embedding-3-small
- Retrieval: Hybrid search (Qdrant vector + PG FTS), RRF fusion (70/30)
- Validation: 5-layer (confidence, multi-model, citation, keywords, feedback)

**Gap Analysis**:
| Component | Current | Target | Gap |
|-----------|---------|--------|-----|
| Chunking | Sentence-based | Hierarchical parent/child | Missing structure awareness |
| Embedding | 1536-dim, short context | 3072-dim, 8K context | Context loss on long passages |
| Indexing | HNSW default | HNSW + PQ + sparse BM25 | No quantization, basic FTS |
| Reranking | None | Cross-encoder top-K | Missing relevance refinement |
| Evaluation | Ad-hoc | Systematic Recall@K, nDCG | No benchmark framework |

---

## Decision

Implement **5-Phase Advanced PDF Embedding Pipeline**:

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    ADVANCED PDF EMBEDDING PIPELINE                       │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌──────────────┐    ┌─────────────────┐    ┌─────────────────────────┐ │
│  │   PDF Input  │───>│   Extraction    │───>│   Advanced Chunking     │ │
│  │              │    │   (3-stage)     │    │                         │ │
│  │  - Rulebook  │    │   ✅ Existing   │    │  ┌─────────────────┐   │ │
│  │  - Quick Ref │    │                 │    │  │ Sentence-Based  │   │ │
│  │  - Setup     │    │  Unstructured   │    │  │ (Baseline)      │   │ │
│  └──────────────┘    │  → SmolDocling  │    │  └────────┬────────┘   │ │
│                      │  → Docnet       │    │           │            │ │
│                      └─────────────────┘    │  ┌────────▼────────┐   │ │
│                                             │  │ Parent/Child    │   │ │
│                                             │  │ Mapping         │   │ │
│                                             │  └────────┬────────┘   │ │
│                                             │           │            │ │
│                                             │  ┌────────▼────────┐   │ │
│                                             │  │ Metadata        │   │ │
│                                             │  │ Enrichment      │   │ │
│                                             │  │ (page, heading, │   │ │
│                                             │  │  element_type)  │   │ │
│                                             │  └─────────────────┘   │ │
│                                             └─────────────────────────┘ │
│                                                         │               │
│                                                         ▼               │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │                    MULTI-PROVIDER EMBEDDING                       │  │
│  │  ┌────────────────┐  ┌────────────────┐  ┌────────────────────┐  │  │
│  │  │   OpenRouter   │  │    Ollama      │  │   HuggingFace      │  │  │
│  │  │                │  │                │  │                    │  │  │
│  │  │ text-embed-3   │  │ nomic-embed    │  │ BGE-M3             │  │  │
│  │  │ -large (3072d) │  │ -text (768d)   │  │ (1024d, multilang) │  │  │
│  │  │                │  │                │  │                    │  │  │
│  │  │ Cloud/Prod     │  │ Local/Dev      │  │ Reranking          │  │  │
│  │  └────────────────┘  └────────────────┘  └────────────────────┘  │  │
│  └──────────────────────────────────────────────────────────────────┘  │
│                                │                                        │
│                                ▼                                        │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │                      HYBRID INDEX LAYER                           │  │
│  │                                                                   │  │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  │  │
│  │  │   Qdrant        │  │   PostgreSQL    │  │   Parent Store  │  │  │
│  │  │                 │  │                 │  │                 │  │  │
│  │  │  HNSW Index     │  │  BM25 Sparse    │  │  Metadata +     │  │  │
│  │  │  + PQ (opt)     │  │  Full-Text      │  │  Parent Chunks  │  │  │
│  │  │                 │  │                 │  │                 │  │  │
│  │  │  Child Chunks   │  │  All Chunks     │  │  Hierarchy Map  │  │  │
│  │  │  (fine-grain)   │  │  (keyword)      │  │  (context)      │  │  │
│  │  └─────────────────┘  └─────────────────┘  └─────────────────┘  │  │
│  └──────────────────────────────────────────────────────────────────┘  │
│                                │                                        │
│                                ▼                                        │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │                    QUERY PIPELINE                                 │  │
│  │                                                                   │  │
│  │  Query ──┬──> Vector Search (40 results) ──┐                     │  │
│  │          │                                  │                     │  │
│  │          └──> BM25 Search (20 results) ────┼──> RRF Fusion       │  │
│  │                                            │    (top 50)         │  │
│  │                                            │                     │  │
│  │                                            ▼                     │  │
│  │                              ┌─────────────────────────┐         │  │
│  │                              │  Cross-Encoder Rerank   │         │  │
│  │                              │  BGE-reranker-v2-m3     │         │  │
│  │                              │  (50 → 10)              │         │  │
│  │                              └───────────┬─────────────┘         │  │
│  │                                          │                       │  │
│  │                                          ▼                       │  │
│  │                              ┌─────────────────────────┐         │  │
│  │                              │  Parent Expansion       │         │  │
│  │                              │  (retrieve full context)│         │  │
│  │                              └───────────┬─────────────┘         │  │
│  │                                          │                       │  │
│  │                                          ▼                       │  │
│  │                              ┌─────────────────────────┐         │  │
│  │                              │  LLM Generation         │         │  │
│  │                              │  (existing pipeline)    │         │  │
│  │                              └─────────────────────────┘         │  │
│  └──────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Implementation Phases

### Phase 0: Dataset Preparation (Prerequisite)

**Objective**: Create evaluation framework with dual dataset strategy

**Deliverables**:
1. **Mozilla Structured QA Integration**
   - Download benchmark from `mozilla-ai/structured-qa`
   - Extract board game rulebook subset (~20-30 questions)
   - Adapt to MeepleAI evaluation format

2. **Custom MeepleAI Dataset**
   - Select 3 representative rulebooks (simple, medium, complex with tables)
   - Create 10-15 Q&A pairs per rulebook (30-45 total)
   - Categories: setup, gameplay, scoring, edge cases
   - Format: `{ question, expected_answer, source_page, difficulty }`

3. **Evaluation Harness**
   - Implement `RagEvaluationService` with metrics:
     - Recall@5, Recall@10
     - nDCG@10
     - MRR (Mean Reciprocal Rank)
     - Answer correctness (LLM-as-judge)
   - Baseline measurement with current pipeline

**Validation Gate**: ≥30 Q&A pairs ready, baseline metrics documented

**Files**:
```
apps/api/src/Api/BoundedContexts/KnowledgeBase/
├── Application/
│   └── Evaluation/
│       ├── Commands/RunEvaluationCommand.cs
│       ├── Queries/GetEvaluationResultsQuery.cs
│       └── Services/RagEvaluationService.cs
├── Domain/
│   └── Evaluation/
│       ├── EvaluationDataset.cs
│       ├── EvaluationResult.cs
│       └── EvaluationMetrics.cs
tests/
└── evaluation-datasets/
    ├── mozilla-structured-qa/
    └── meepleai-custom/
```

---

### Phase 1: Advanced Chunking (Sentence-Based Baseline + Parent/Child)

**Objective**: Implement hierarchical chunking with parent/child mapping

**Strategy Selection**: Sentence-based first (user choice)

**Chunking Configurations**:

| Config | Chunk Size | Overlap | Use Case |
|--------|------------|---------|----------|
| **Baseline** | 350 tokens | 15% (52 tokens) | General rulebooks |
| **Dense** | 200 tokens | 20% (40 tokens) | Complex tables/lists |
| **Sparse** | 500 tokens | 10% (50 tokens) | Narrative sections |

**Parent/Child Architecture**:
```csharp
public class HierarchicalChunk
{
    public string Id { get; set; }                    // UUID
    public string ParentId { get; set; }              // null for root
    public List<string> ChildIds { get; set; }        // child chunk refs
    public string Content { get; set; }               // chunk text
    public int Level { get; set; }                    // 0=section, 1=paragraph, 2=sentence
    public ChunkMetadata Metadata { get; set; }       // page, heading, bbox, element_type
}

public class ChunkMetadata
{
    public int Page { get; set; }
    public string Heading { get; set; }               // nearest section heading
    public BoundingBox? BBox { get; set; }            // optional spatial info
    public string ElementType { get; set; }           // text, table, list, heading
    public string GameId { get; set; }
    public string DocumentId { get; set; }
}
```

**Implementation**:
```csharp
// BoundedContexts/KnowledgeBase/Application/Services/AdvancedChunkingService.cs
public interface IAdvancedChunkingService
{
    Task<List<HierarchicalChunk>> ChunkDocumentAsync(
        ExtractedDocument document,
        ChunkingConfiguration config,
        CancellationToken ct = default);
}

public class AdvancedChunkingService : IAdvancedChunkingService
{
    private readonly ILogger<AdvancedChunkingService> _logger;

    public async Task<List<HierarchicalChunk>> ChunkDocumentAsync(
        ExtractedDocument document,
        ChunkingConfiguration config,
        CancellationToken ct = default)
    {
        var chunks = new List<HierarchicalChunk>();

        // Step 1: Section detection (headings from Unstructured metadata)
        var sections = ExtractSections(document);

        // Step 2: For each section, create parent chunk
        foreach (var section in sections)
        {
            var parentChunk = CreateParentChunk(section);
            chunks.Add(parentChunk);

            // Step 3: Split into child chunks (sentence-based)
            var childChunks = await SplitIntoChildChunksAsync(
                section.Content,
                config,
                parentChunk.Id,
                ct);

            parentChunk.ChildIds = childChunks.Select(c => c.Id).ToList();
            chunks.AddRange(childChunks);
        }

        return chunks;
    }
}
```

**Validation Gate**: Unit tests pass, parent/child relationships correct

**Files**:
```
apps/api/src/Api/BoundedContexts/KnowledgeBase/
├── Application/
│   └── Services/
│       └── AdvancedChunkingService.cs
├── Domain/
│   ├── Chunking/
│   │   ├── HierarchicalChunk.cs
│   │   ├── ChunkMetadata.cs
│   │   └── ChunkingConfiguration.cs
│   └── Services/
│       └── ChunkingStrategySelector.cs
└── Infrastructure/
    └── Repositories/
        └── ChunkRepository.cs
```

---

### Phase 2: Multi-Provider Embedding

**Objective**: Flexible embedding with long-context support

**Provider Matrix**:

| Provider | Model | Dimensions | Context | Use Case | Cost |
|----------|-------|------------|---------|----------|------|
| **OpenRouter** | text-embedding-3-large | 3072 | 8K | Production | $0.13/M tokens |
| **OpenRouter** | text-embedding-3-small | 1536 | 8K | Budget | $0.02/M tokens |
| **Ollama** | nomic-embed-text | 768 | 8K | Local dev | Free |
| **Ollama** | mxbai-embed-large | 1024 | 512 | Fallback | Free |
| **HuggingFace** | BGE-M3 | 1024 | 8K | Multilingual | Free |

**Multi-Provider Architecture**:
```csharp
public interface IEmbeddingProvider
{
    string ProviderName { get; }
    int Dimensions { get; }
    int MaxContextTokens { get; }
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct);
    Task<List<float[]>> GenerateBatchEmbeddingsAsync(List<string> texts, CancellationToken ct);
}

public class EmbeddingProviderFactory
{
    public IEmbeddingProvider GetProvider(EmbeddingProviderType type)
    {
        return type switch
        {
            EmbeddingProviderType.OpenRouterLarge => new OpenRouterEmbeddingProvider("text-embedding-3-large"),
            EmbeddingProviderType.OpenRouterSmall => new OpenRouterEmbeddingProvider("text-embedding-3-small"),
            EmbeddingProviderType.OllamaNomic => new OllamaEmbeddingProvider("nomic-embed-text"),
            EmbeddingProviderType.OllamaMxbai => new OllamaEmbeddingProvider("mxbai-embed-large"),
            EmbeddingProviderType.HuggingFaceBGE => new HuggingFaceEmbeddingProvider("BAAI/bge-m3"),
            _ => throw new ArgumentException($"Unknown provider: {type}")
        };
    }
}
```

**Configuration**:
```json
{
  "Embedding": {
    "Provider": "OpenRouterLarge",
    "FallbackProvider": "OllamaNomic",
    "BatchSize": 100,
    "MaxRetries": 3,
    "TimeoutSeconds": 30
  }
}
```

**Validation Gate**: Integration tests pass for all providers

**Files**:
```
apps/api/src/Api/BoundedContexts/KnowledgeBase/
├── Infrastructure/
│   └── Embedding/
│       ├── IEmbeddingProvider.cs
│       ├── EmbeddingProviderFactory.cs
│       ├── Providers/
│       │   ├── OpenRouterEmbeddingProvider.cs
│       │   ├── OllamaEmbeddingProvider.cs
│       │   └── HuggingFaceEmbeddingProvider.cs
│       └── EmbeddingConfiguration.cs
```

---

### Phase 3: Optimized Hybrid Indexing

**Objective**: Tune HNSW, add PQ quantization, optimize BM25 sparse index

**Qdrant HNSW Tuning**:
```json
{
  "vectors": {
    "size": 3072,
    "distance": "Cosine"
  },
  "hnsw_config": {
    "m": 16,
    "ef_construct": 100,
    "full_scan_threshold": 10000
  },
  "quantization_config": {
    "scalar": {
      "type": "int8",
      "quantile": 0.99,
      "always_ram": true
    }
  }
}
```

**Quantization Options**:

| Type | Memory Reduction | Accuracy Loss | Use Case |
|------|------------------|---------------|----------|
| **None** | 0% | 0% | Small collections (<100K) |
| **Scalar (int8)** | 75% | <1% | Medium (100K-1M) |
| **Product (PQ)** | 90% | 2-5% | Large (>1M) |

**BM25 Sparse Index** (PostgreSQL):
```sql
-- Enhanced FTS configuration for Italian board game terms
CREATE TEXT SEARCH CONFIGURATION meepleai_italian (COPY = italian);

-- Add game-specific synonyms
ALTER TEXT SEARCH CONFIGURATION meepleai_italian
    ALTER MAPPING FOR word WITH italian_stem, italian_synonym;

-- Create GIN index for fast BM25-style retrieval
CREATE INDEX idx_chunks_fts ON chunks
    USING GIN (to_tsvector('meepleai_italian', content));

-- Materialized view for pre-computed BM25 scores (optional)
CREATE MATERIALIZED VIEW chunks_bm25 AS
SELECT
    id,
    content,
    ts_rank_cd(to_tsvector('meepleai_italian', content), query) AS bm25_score
FROM chunks, plainto_tsquery('meepleai_italian', '') AS query;
```

**Metadata Index**:
```csharp
public class ChunkIndexEntry
{
    public string ChunkId { get; set; }
    public string ParentId { get; set; }
    public float[] Embedding { get; set; }
    public Dictionary<string, object> Payload { get; set; }
    // Payload includes: page, heading, element_type, game_id, document_id
}
```

**Validation Gate**: Benchmark baseline vs optimized (latency, storage)

---

### Phase 4: Cross-Encoder Reranking Pipeline

**Objective**: Add reranking layer for improved relevance

**Reranker Options**:

| Model | Type | Accuracy | Latency | Memory |
|-------|------|----------|---------|--------|
| **BGE-reranker-v2-m3** | Cross-encoder | Best | ~50ms/pair | 1.5GB |
| **ms-marco-MiniLM-L6** | Cross-encoder | Good | ~20ms/pair | 400MB |
| **ColBERT-v2** | Late-interaction | Good | ~10ms/pair | 800MB |

**Recommended**: BGE-reranker-v2-m3 (best accuracy, acceptable latency)

**Reranking Pipeline**:
```csharp
public class RerankedRetrievalService : IRerankedRetrievalService
{
    private readonly IHybridSearchService _hybridSearch;
    private readonly ICrossEncoderReranker _reranker;
    private readonly IParentChunkResolver _parentResolver;

    public async Task<List<RankedChunk>> RetrieveAndRerankAsync(
        string query,
        RetrievalOptions options,
        CancellationToken ct = default)
    {
        // Step 1: Hybrid retrieval (vector + BM25)
        var candidates = await _hybridSearch.SearchAsync(
            query,
            vectorCount: options.VectorCount,    // 40
            bm25Count: options.Bm25Count,        // 20
            ct);

        // Step 2: RRF fusion
        var fused = RrfFusion(candidates, k: 60);
        var topCandidates = fused.Take(options.RerankPoolSize).ToList();  // 50

        // Step 3: Cross-encoder reranking
        var reranked = await _reranker.RerankAsync(query, topCandidates, ct);
        var topReranked = reranked.Take(options.FinalCount).ToList();     // 10

        // Step 4: Parent expansion (retrieve full context)
        var expanded = await _parentResolver.ExpandToParentsAsync(topReranked, ct);

        return expanded;
    }
}
```

**Integration with sentence-transformers**:
```python
# Python service for reranking (called via HTTP or gRPC)
from sentence_transformers import CrossEncoder

model = CrossEncoder('BAAI/bge-reranker-v2-m3')

def rerank(query: str, chunks: list[dict]) -> list[dict]:
    pairs = [(query, chunk['content']) for chunk in chunks]
    scores = model.predict(pairs)

    for i, chunk in enumerate(chunks):
        chunk['rerank_score'] = float(scores[i])

    return sorted(chunks, key=lambda x: x['rerank_score'], reverse=True)
```

**C# Integration** (HTTP client to Python service):
```csharp
public class CrossEncoderRerankerClient : ICrossEncoderReranker
{
    private readonly HttpClient _httpClient;

    public async Task<List<RankedChunk>> RerankAsync(
        string query,
        List<RetrievedChunk> chunks,
        CancellationToken ct)
    {
        var request = new RerankRequest { Query = query, Chunks = chunks };
        var response = await _httpClient.PostAsJsonAsync("/rerank", request, ct);
        return await response.Content.ReadFromJsonAsync<List<RankedChunk>>(ct);
    }
}
```

**Validation Gate**: Recall@10 ≥60% on evaluation dataset

---

### Phase 5: Evaluation & Documentation

**Objective**: Systematic benchmarking and documentation

**Evaluation Matrix**:

| Metric | Baseline | Target | Method |
|--------|----------|--------|--------|
| Recall@5 | TBD | ≥60% | Top-5 contains correct chunk |
| Recall@10 | TBD | ≥70% | Top-10 contains correct chunk |
| nDCG@10 | TBD | ≥0.65 | Graded relevance ranking |
| MRR | TBD | ≥0.55 | Mean reciprocal rank |
| P95 Latency | ~2s | <1.5s | End-to-end query time |
| Storage/page | TBD | Documented | Bytes per PDF page |

**Grid Search Configurations**:
```yaml
chunking:
  - { size: 200, overlap: 0.20, name: "dense" }
  - { size: 350, overlap: 0.15, name: "baseline" }
  - { size: 500, overlap: 0.10, name: "sparse" }

quantization:
  - { type: "none", name: "full_precision" }
  - { type: "scalar_int8", name: "quantized" }

reranking:
  - { enabled: false, name: "no_rerank" }
  - { enabled: true, model: "bge-reranker-v2-m3", name: "bge_rerank" }
```

**Benchmark Report Template**:
```markdown
## Evaluation Report - [Date]

### Configuration
- Chunking: [config]
- Embedding: [model]
- Quantization: [type]
- Reranking: [enabled/disabled]

### Metrics
| Dataset | Recall@5 | Recall@10 | nDCG@10 | MRR | P95 Latency |
|---------|----------|-----------|---------|-----|-------------|
| Mozilla | X% | X% | X.XX | X.XX | Xms |
| Custom | X% | X% | X.XX | X.XX | Xms |

### Observations
- [Key findings]

### Recommendations
- [Next steps]
```

**Observability Checklist**:
- [ ] Retrieval trace logging (query → chunks → rerank → generation)
- [ ] Prometheus metrics (retrieval_latency_ms, rerank_latency_ms, chunks_retrieved)
- [ ] PII redaction pre-embedding (player names, email patterns)
- [ ] Error handling for failed embeddings

**Validation Gate**: Report with ≥3 configurations compared, Recall@10 ≥70%

---

## Consequences

### Positive

**Improved Retrieval Quality**:
- Parent/child chunking preserves context
- Reranking refines relevance (expected +10-15% Recall)
- Long-context embedding captures cross-references

**Flexibility**:
- Multi-provider embedding (cost vs quality tradeoff)
- Configurable chunking strategies per document type
- Quantization options for scaling

**Measurability**:
- Systematic evaluation framework
- Baseline → improvement tracking
- Data-driven optimization

### Negative (Trade-offs Accepted)

**Increased Latency** (+200-500ms for reranking):
- Mitigation: See Latency Optimization Strategy below

**Complexity** (+40% codebase in KnowledgeBase):
- Mitigation: Modular services, comprehensive tests, clear documentation

**Infrastructure**:
- Python service for reranking (containerized)
- Ollama for local embedding (already in docker-compose)

**Cost Increase** (embedding model upgrade):
- text-embedding-3-small → text-embedding-3-large = 6.5x cost increase
- Mitigation: Use large model for production, small/Ollama for dev
- Volume estimate: ~10K queries/month × $0.13/M tokens ≈ $1.30/month (negligible)
- ROI: +10-15% accuracy improvement justifies minimal cost increase

---

## Latency Optimization Strategy

### Problem
Adding reranking risks exceeding P95 <1.5s target:

| Component | Sequential Time |
|-----------|----------------|
| Query embedding | ~50ms |
| Vector search (40) | ~50ms |
| BM25 search (20) | ~30ms |
| RRF fusion | ~10ms |
| Reranking (50 docs) | ~500ms |
| Parent expansion | ~50ms |
| LLM generation | ~800ms |
| **Total** | **~1,490ms** |

### Optimization Strategies

**1. Parallel Execution**
```csharp
// Vector and BM25 search in parallel
var (vectorResults, bm25Results) = await (
    _vectorSearch.SearchAsync(query, 40, ct),
    _bm25Search.SearchAsync(query, 20, ct)
).WhenAll();
```

**2. Reranker Result Caching**
```csharp
// Cache rerank scores for query+chunk pairs (5-minute TTL)
var cacheKey = $"rerank:{query.GetHashCode()}:{chunk.Id}";
var cachedScore = await _cache.GetAsync<float?>(cacheKey);
if (cachedScore.HasValue) return cachedScore.Value;
```
Expected cache hit rate: 30-50% for common game rules questions

**3. Adaptive Reranking**
```csharp
// Skip reranking for high-confidence queries
if (topCandidate.Score >= 0.90m)
{
    _logger.LogInformation("Skipping rerank, high confidence: {Score}", topCandidate.Score);
    return candidates.Take(options.FinalCount).ToList();
}
```

**4. Batched Parent Expansion**
```csharp
// Single DB query for all parent chunks
var parentIds = rerankedChunks.Select(c => c.ParentId).Distinct();
var parents = await _chunkRepo.GetByIdsAsync(parentIds, ct);
```

### Optimized Latency Budget

| Component | Sequential | Optimized |
|-----------|------------|-----------|
| Query embedding | 50ms | 50ms |
| Vector + BM25 search | 80ms | **50ms** (parallel) |
| RRF fusion | 10ms | 10ms |
| Reranking | 500ms | **300ms** (40% cached) |
| Parent expansion | 50ms | **20ms** (batched) |
| LLM generation | 800ms | 800ms |
| **Total** | **1,490ms** | **1,230ms** ✅ |

### Feature Flags
```json
{
  "Retrieval": {
    "EnableReranking": true,
    "RerankCacheTtlSeconds": 300,
    "SkipRerankThreshold": 0.90,
    "EnableParallelSearch": true
  }
}
```

---

## Interface Contracts

### Core Interfaces

```csharp
/// <summary>
/// Resolves child chunks to their parent chunks for context expansion.
/// </summary>
public interface IParentChunkResolver
{
    /// <summary>
    /// Expands ranked child chunks to include parent context.
    /// </summary>
    Task<List<HierarchicalChunk>> ExpandToParentsAsync(
        List<RankedChunk> childChunks,
        CancellationToken ct = default);

    /// <summary>
    /// Gets parent chunk by child ID.
    /// </summary>
    Task<HierarchicalChunk?> GetParentAsync(
        string childId,
        CancellationToken ct = default);
}

/// <summary>
/// Cross-encoder reranking service for improving retrieval relevance.
/// </summary>
public interface ICrossEncoderReranker
{
    /// <summary>
    /// Reranks retrieved chunks using cross-encoder model.
    /// </summary>
    Task<List<RankedChunk>> RerankAsync(
        string query,
        List<RetrievedChunk> chunks,
        CancellationToken ct = default);

    /// <summary>
    /// Scores a single query-chunk pair.
    /// </summary>
    Task<float> ScoreAsync(
        string query,
        string chunkContent,
        CancellationToken ct = default);

    /// <summary>
    /// Health check for reranker service.
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}

/// <summary>
/// Repository for hierarchical chunk storage and retrieval.
/// </summary>
public interface IChunkRepository
{
    Task<HierarchicalChunk?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<List<HierarchicalChunk>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct = default);
    Task<List<HierarchicalChunk>> GetChildrenAsync(string parentId, CancellationToken ct = default);
    Task SaveAsync(HierarchicalChunk chunk, CancellationToken ct = default);
    Task SaveBatchAsync(List<HierarchicalChunk> chunks, CancellationToken ct = default);
    Task DeleteByDocumentIdAsync(string documentId, CancellationToken ct = default);
}

/// <summary>
/// RAG evaluation service for benchmarking retrieval quality.
/// </summary>
public interface IRagEvaluationService
{
    Task<EvaluationResult> EvaluateAsync(
        EvaluationDataset dataset,
        EvaluationOptions options,
        CancellationToken ct = default);

    Task<Dictionary<string, double>> ComputeMetricsAsync(
        List<EvaluationSample> samples,
        CancellationToken ct = default);
}

/// <summary>
/// Reranked retrieval orchestrator (wraps hybrid search + reranking).
/// </summary>
public interface IRerankedRetrievalService
{
    Task<List<RankedChunk>> RetrieveAndRerankAsync(
        string query,
        RetrievalOptions options,
        CancellationToken ct = default);
}
```

### Data Transfer Objects

```csharp
public record RankedChunk
{
    public required string Id { get; init; }
    public required string Content { get; init; }
    public required float Score { get; init; }
    public float? RerankScore { get; init; }
    public string? ParentId { get; init; }
    public ChunkMetadata? Metadata { get; init; }
}

public record RetrievalOptions
{
    public int VectorCount { get; init; } = 40;
    public int Bm25Count { get; init; } = 20;
    public int RerankPoolSize { get; init; } = 50;
    public int FinalCount { get; init; } = 10;
    public bool EnableReranking { get; init; } = true;
    public bool ExpandToParents { get; init; } = true;
}

public record EvaluationSample
{
    public required string Query { get; init; }
    public required string ExpectedAnswer { get; init; }
    public required List<string> RelevantChunkIds { get; init; }
    public List<RankedChunk>? RetrievedChunks { get; init; }
    public string? GeneratedAnswer { get; init; }
}

public record EvaluationResult
{
    public required double RecallAt5 { get; init; }
    public required double RecallAt10 { get; init; }
    public required double NdcgAt10 { get; init; }
    public required double Mrr { get; init; }
    public required double P95LatencyMs { get; init; }
    public required DateTime EvaluatedAt { get; init; }
    public required string Configuration { get; init; }
}
```

---

## Error Handling Matrix

| Failure Scenario | Detection | Fallback Behavior | Logging |
|-----------------|-----------|-------------------|---------|
| **Embedding provider timeout** | `HttpRequestException` / `TaskCanceledException` | Use `FallbackProvider` from config | `Warning` + switch metric |
| **Embedding provider rate limit** | HTTP 429 | Exponential backoff (3 retries) → fallback | `Warning` + retry count |
| **Reranker service unavailable** | Health check fail / connection refused | Skip reranking, use RRF scores only | `Warning` + feature flag |
| **Reranker timeout** | `TaskCanceledException` after 5s | Return unranked results | `Warning` + latency |
| **Parent chunk not found** | Null result from repository | Return child chunk with `ParentMissing` flag | `Warning` + chunk ID |
| **Vector search failure** | Qdrant connection error | Fall back to BM25-only search | `Error` + degrade metric |
| **BM25 search failure** | PostgreSQL error | Fall back to vector-only search | `Error` + degrade metric |
| **Both searches fail** | Both above | Return empty results + user message | `Critical` + alert |
| **Quality below threshold** | Score < 0.60 after reranking | Add disclaimer to response | `Info` + low_confidence metric |
| **Empty retrieval results** | Zero chunks returned | Return "information not found" message | `Warning` + query logged |

### Error Handling Implementation

```csharp
public class ResilientRetrievalService : IRerankedRetrievalService
{
    private readonly IHybridSearchService _hybridSearch;
    private readonly ICrossEncoderReranker _reranker;
    private readonly ILogger<ResilientRetrievalService> _logger;

    public async Task<List<RankedChunk>> RetrieveAndRerankAsync(
        string query,
        RetrievalOptions options,
        CancellationToken ct = default)
    {
        // Step 1: Hybrid retrieval with fallback
        List<RetrievedChunk> candidates;
        try
        {
            candidates = await _hybridSearch.SearchAsync(query, options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hybrid search failed, attempting degraded search");
            candidates = await FallbackSearchAsync(query, options, ct);
        }

        if (candidates.Count == 0)
        {
            _logger.LogWarning("No chunks retrieved for query: {Query}", query);
            return new List<RankedChunk>();
        }

        // Step 2: Reranking with graceful degradation
        if (options.EnableReranking && await _reranker.IsHealthyAsync(ct))
        {
            try
            {
                return await _reranker.RerankAsync(query, candidates, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reranking failed, returning RRF-ranked results");
                // Fall through to return candidates without reranking
            }
        }

        return candidates.Select(c => new RankedChunk
        {
            Id = c.Id,
            Content = c.Content,
            Score = c.Score,
            RerankScore = null,  // Indicates reranking was skipped
            ParentId = c.ParentId,
            Metadata = c.Metadata
        }).ToList();
    }
}
```

---

## Service Relationships

### Integration Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    EXISTING SERVICES                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  RagService (orchestrator)                                       │
│      │                                                           │
│      ├── IHybridSearchService (existing - unchanged)             │
│      │       ├── IVectorSearchService (Qdrant)                   │
│      │       └── IBm25SearchService (PostgreSQL FTS)             │
│      │                                                           │
│      ├── ILlmService (existing - unchanged)                      │
│      │                                                           │
│      └── IValidationPipeline (existing - unchanged)              │
│              └── 5-layer validation from ADR-001                 │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ EXTENDS (non-breaking)
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    NEW SERVICES (ADR-016)                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  IRerankedRetrievalService (NEW - optional wrapper)              │
│      │                                                           │
│      ├── IHybridSearchService (existing - reused)                │
│      │                                                           │
│      ├── ICrossEncoderReranker (NEW)                             │
│      │       └── Python reranker microservice                    │
│      │                                                           │
│      └── IParentChunkResolver (NEW)                              │
│              └── IChunkRepository (NEW)                          │
│                                                                  │
│  IAdvancedChunkingService (NEW - extends TextChunkingService)    │
│      │                                                           │
│      ├── TextChunkingService (existing - sentence detection)     │
│      │                                                           │
│      └── Parent/child hierarchy mapping (NEW)                    │
│                                                                  │
│  IEmbeddingProvider (NEW - multi-provider abstraction)           │
│      │                                                           │
│      ├── OpenRouterEmbeddingProvider                             │
│      ├── OllamaEmbeddingProvider                                 │
│      └── HuggingFaceEmbeddingProvider                            │
│                                                                  │
│  IRagEvaluationService (NEW - benchmarking)                      │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Feature Flag Integration

```csharp
// RagService can use either path based on config
public class RagService : IRagService
{
    private readonly IHybridSearchService _hybridSearch;
    private readonly IRerankedRetrievalService _rerankedRetrieval;
    private readonly IOptions<RetrievalConfiguration> _config;

    public async Task<RagResponse> AskAsync(string query, CancellationToken ct)
    {
        // Choose retrieval strategy based on feature flag
        var chunks = _config.Value.EnableAdvancedRetrieval
            ? await _rerankedRetrieval.RetrieveAndRerankAsync(query, ct)
            : await _hybridSearch.SearchAsync(query, ct);

        // Rest of pipeline unchanged...
    }
}
```

### Backward Compatibility

| Component | Breaking Change | Migration |
|-----------|-----------------|-----------|
| `RagService` | No | Feature flag controls new path |
| `TextChunkingService` | No | `AdvancedChunkingService` wraps it |
| `IHybridSearchService` | No | Reused by new services |
| Qdrant schema | No | New fields are optional (nullable) |
| PostgreSQL schema | No | New tables, no changes to existing |

---

## Stack Decision: Hybrid Internal + Libraries

Based on brainstorming analysis:

| Component | Choice | Rationale |
|-----------|--------|-----------|
| Orchestration | Internal (`RagService`) | Full control, existing patterns |
| Chunking | LangChain `text_splitters` | Mature, well-tested |
| Embedding | Internal multi-provider | Matches existing OpenRouter pattern |
| Reranking | `sentence-transformers` | Industry standard, MIT license |
| Evaluation | `ragas` + custom | Standard metrics + MeepleAI-specific |

**Cost**: All components are open source (MIT/Apache 2.0)

---

## Alternatives Considered

### Alternative 1: Full LlamaIndex Integration
**Pros**: Batteries-included RAG framework
**Cons**: Lock-in, less control, learning curve
**Decision**: Rejected - prefer surgical library adoption

### Alternative 2: ColBERT Late Interaction
**Pros**: Faster reranking, token-level matching
**Cons**: Complex indexing, higher storage
**Decision**: Deferred - evaluate if BGE insufficient

### Alternative 3: Proposition Chunking
**Pros**: Atomic facts, better for complex reasoning
**Cons**: Requires LLM pass, expensive at scale
**Decision**: Phase 6 if baseline insufficient

---

## Success Criteria

**Phase Completion**:
- [ ] Phase 0: ≥30 Q&A pairs, baseline documented
- [ ] Phase 1: Chunking tests pass, parent/child working
- [ ] Phase 2: All embedding providers operational
- [ ] Phase 3: Index benchmarks documented
- [ ] Phase 4: Recall@10 ≥60%
- [ ] Phase 5: Final Recall@10 ≥70%, P95 <1.5s

**Acceptance Criteria (from Issue #1901)**:
- [ ] Parse 3 PDF types with layout metadata
- [ ] 70% Q&A accuracy on test set
- [ ] Compare ≥3 chunking configs
- [ ] P95 <1.5s on 10 queries

---

## References

- Issue #1901: Original requirements
- ADR-001: Hybrid RAG foundation
- ADR-003b: PDF extraction pipeline
- Mozilla Structured QA: https://github.com/mozilla-ai/structured-qa
- Open RAG Benchmark: https://huggingface.co/datasets/vectara/open_ragbench
- BGE Reranker: https://huggingface.co/BAAI/bge-reranker-v2-m3

---

**ADR Metadata**:
- **ID**: ADR-016
- **Status**: Proposed
- **Date**: 2025-12-03
- **Supersedes**: None
- **Related**: ADR-001, ADR-003b
