# ADR-024: Advanced PDF Embedding Pipeline with Hybrid Indexing

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
1. **Mozilla Structured QA**: Board game subset (~20-30 questions)
2. **Custom MeepleAI Dataset**: 30-45 Q&A pairs across 3 rulebooks (simple/medium/complex)
3. **Evaluation Harness**: Recall@5/10, nDCG@10, MRR, LLM-as-judge correctness

**Validation Gate**: ≥30 Q&A pairs ready, baseline metrics documented

**Implementation Reference**: See `tests/evaluation-datasets/` for dataset files, `RagEvaluationService.cs` for harness implementation

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
- `HierarchicalChunk`: ID, ParentID, ChildIDs, Content, Level (0=section, 1=paragraph, 2=sentence)
- `ChunkMetadata`: Page, Heading, BoundingBox, ElementType (text/table/list), GameID, DocumentID

**Implementation Strategy**:
1. Section detection from Unstructured metadata headings
2. Parent chunk creation per section
3. Child chunks via sentence-based splitting
4. Bidirectional parent/child ID mapping

**Validation Gate**: Unit tests pass, parent/child relationships correct

**Test Reference**: See `AdvancedChunkingServiceTests.cs` for parent/child mapping validation

**Implementation Files**: See `AdvancedChunkingService.cs`, `HierarchicalChunk.cs`, `ChunkRepository.cs` in KnowledgeBase bounded context

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
- `IEmbeddingProvider`: Abstract interface with ProviderName, Dimensions, MaxContextTokens, GenerateEmbedding methods
- `EmbeddingProviderFactory`: Factory pattern for provider instantiation (OpenRouter, Ollama, HuggingFace)

**Configuration Strategy**:
- Primary provider: OpenRouterLarge (production)
- Fallback provider: OllamaNomic (dev/offline)
- Batch size: 100, max retries: 3, timeout: 30s

**Validation Gate**: Integration tests pass for all providers

**Test Reference**: See `EmbeddingProviderTests.cs` for provider-specific tests, `EmbeddingProviderFactoryTests.cs` for factory pattern validation

**Implementation Files**: See `Infrastructure/Embedding/` directory for provider implementations

---

### Phase 3: Optimized Hybrid Indexing

**Objective**: Tune HNSW, add PQ quantization, optimize BM25 sparse index

**Qdrant HNSW Configuration**:
- Vector size: 3072, distance: Cosine
- HNSW: m=16, ef_construct=100, full_scan_threshold=10000
- Quantization: Scalar int8 (75% memory reduction, <1% accuracy loss) for medium collections (100K-1M chunks)

**PostgreSQL BM25 Index**:
- Custom text search config: `meepleai_italian` with Italian stemming + synonyms
- GIN index on `to_tsvector('meepleai_italian', content)`
- Optional: Materialized view for pre-computed BM25 scores

**Metadata Payload**: page, heading, element_type, game_id, document_id

**Validation Gate**: Benchmark baseline vs optimized (latency, storage)

**Configuration Reference**: See `qdrant-collections.json` and migration `AddChunksFtsIndex.sql`

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
1. **Hybrid Retrieval**: Vector (40 results) + BM25 (20 results)
2. **RRF Fusion**: Combine to top 50 candidates (k=60)
3. **Cross-Encoder Reranking**: BGE-reranker-v2-m3 scores top 50 → select top 10
4. **Parent Expansion**: Retrieve full context for final chunks

**Python Reranker Service**:
- Model: `sentence-transformers` CrossEncoder (BAAI/bge-reranker-v2-m3)
- API: HTTP endpoint `/rerank` with JSON request/response
- Integration: C# `CrossEncoderRerankerClient` via HttpClient

**Validation Gate**: Recall@10 ≥60% on evaluation dataset

**Test Reference**: See `RerankedRetrievalServiceTests.cs` for pipeline integration, `CrossEncoderRerankerTests.cs` for Python service interaction

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
- Chunking: dense (200/20%), baseline (350/15%), sparse (500/10%)
- Quantization: none, scalar_int8
- Reranking: disabled, BGE-reranker-v2-m3

**Evaluation Outputs**:
- Benchmark reports with configuration matrix
- Prometheus metrics: retrieval_latency_ms, rerank_latency_ms, chunks_retrieved
- Retrieval trace logging for debugging
- PII redaction pre-embedding

**Report Template Reference**: See `docs/evaluation-reports/` for completed benchmarks

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

**1. Parallel Execution**: Run vector and BM25 searches concurrently (saves ~30ms)

**2. Reranker Result Caching**: Cache query+chunk scores for 5 minutes (30-50% hit rate expected)

**3. Adaptive Reranking**: Skip reranking when top candidate score ≥0.90 (high confidence)

**4. Batched Parent Expansion**: Single DB query for all parent chunks (prevents N+1 queries)

**Implementation Reference**: See `LatencyOptimizationTests.cs` for parallel execution benchmarks

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

**IParentChunkResolver**:
- `ExpandToParentsAsync()`: Expands child chunks to include parent context
- `GetParentAsync()`: Retrieves parent chunk by child ID

**ICrossEncoderReranker**:
- `RerankAsync()`: Reranks chunks using cross-encoder model
- `ScoreAsync()`: Scores single query-chunk pair
- `IsHealthyAsync()`: Health check for reranker service

**IChunkRepository**:
- CRUD operations: `GetByIdAsync()`, `GetByIdsAsync()`, `SaveAsync()`, `SaveBatchAsync()`
- Navigation: `GetChildrenAsync()`, `DeleteByDocumentIdAsync()`

**IRagEvaluationService**:
- `EvaluateAsync()`: Runs benchmarks on evaluation dataset
- `ComputeMetricsAsync()`: Calculates Recall@K, nDCG, MRR

**IRerankedRetrievalService**:
- `RetrieveAndRerankAsync()`: Orchestrates hybrid search + reranking + parent expansion

### Key Data Structures

**RankedChunk**: Id, Content, Score, RerankScore, ParentId, Metadata

**RetrievalOptions**: VectorCount (40), Bm25Count (20), RerankPoolSize (50), FinalCount (10), EnableReranking, ExpandToParents

**EvaluationResult**: RecallAt5, RecallAt10, NdcgAt10, Mrr, P95LatencyMs, Configuration

**Contract Reference**: See interface files in `BoundedContexts/KnowledgeBase/Application/Interfaces/`

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

### Error Handling Strategy

**Resilience Pattern**:
1. Hybrid search failure → Fallback to single-mode search (vector-only or BM25-only)
2. Reranker unavailable → Skip reranking, use RRF scores (set `RerankScore = null`)
3. Parent chunk missing → Return child with `ParentMissing` flag
4. Empty results → Log warning, return empty list with user message

**Graceful Degradation**: Service continues with reduced functionality rather than failing completely

**Error Handling Reference**: See `ResilientRetrievalServiceTests.cs` for failure scenarios

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

**Strategy Selection**:
- `EnableAdvancedRetrieval = true`: Use reranked retrieval path
- `EnableAdvancedRetrieval = false`: Use basic hybrid search (baseline)

**Backward Compatibility**: Existing `RagService` routes to either pipeline based on configuration flag, no breaking changes

**Feature Flag Reference**: See `appsettings.json` Retrieval section

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
