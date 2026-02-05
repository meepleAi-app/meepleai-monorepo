# Built-in Plugins

> **Reference Documentation for Pre-built RAG Plugins**

MeepleAI includes a library of ready-to-use plugins covering common RAG operations. This directory documents each plugin's purpose, configuration, and usage.

## Plugin Categories

| Category | Description | Plugins |
|----------|-------------|---------|
| [Routing](routing.md) | Query classification and path selection | Intent Router, Complexity Router |
| [Cache](cache.md) | Result caching for performance | Semantic Cache, Exact Match Cache |
| [Retrieval](retrieval.md) | Document fetching from stores | Vector Search, Hybrid Search, Keyword Search |
| [Evaluation](evaluation.md) | Quality assessment | Relevance Scorer, Confidence Evaluator |
| [Generation](generation.md) | Response creation | Answer Generator, Summary Generator |
| [Validation](validation.md) | Output verification | Hallucination Detector, Guardrails |
| [Transform](transform.md) | Data modification | Query Rewriter, Document Reranker |
| [Filter](filter.md) | Document selection | Deduplication, Threshold Filter |

## Quick Reference

### Routing Plugins

| Plugin ID | Name | Purpose |
|-----------|------|---------|
| `routing-intent-v1` | Intent Router | Classify queries by type (rules, strategy, general) |
| `routing-complexity-v1` | Complexity Router | Route based on query complexity |

### Cache Plugins

| Plugin ID | Name | Purpose |
|-----------|------|---------|
| `cache-semantic-v1` | Semantic Cache | Cache by semantic similarity |
| `cache-exact-v1` | Exact Match Cache | Cache by exact query match |

### Retrieval Plugins

| Plugin ID | Name | Purpose |
|-----------|------|---------|
| `retrieval-vector-v1` | Vector Search | Similarity search in vector store |
| `retrieval-hybrid-v1` | Hybrid Search | Combined vector + keyword search |
| `retrieval-keyword-v1` | Keyword Search | Traditional BM25 search |

### Evaluation Plugins

| Plugin ID | Name | Purpose |
|-----------|------|---------|
| `evaluation-relevance-v1` | Relevance Scorer | Score document relevance to query |
| `evaluation-confidence-v1` | Confidence Evaluator | Overall retrieval confidence |

### Generation Plugins

| Plugin ID | Name | Purpose |
|-----------|------|---------|
| `generation-answer-v1` | Answer Generator | Generate answers from documents |
| `generation-summary-v1` | Summary Generator | Summarize retrieved documents |

### Validation Plugins

| Plugin ID | Name | Purpose |
|-----------|------|---------|
| `validation-hallucination-v1` | Hallucination Detector | Detect unsupported claims |
| `validation-guardrails-v1` | Guardrails | Content safety checks |

### Transform Plugins

| Plugin ID | Name | Purpose |
|-----------|------|---------|
| `transform-rewrite-v1` | Query Rewriter | Improve query for retrieval |
| `transform-rerank-v1` | Document Reranker | Re-order documents by relevance |

### Filter Plugins

| Plugin ID | Name | Purpose |
|-----------|------|---------|
| `filter-dedupe-v1` | Deduplication | Remove duplicate documents |
| `filter-threshold-v1` | Threshold Filter | Filter by score threshold |

## Usage in Visual Builder

1. Open the Visual Pipeline Builder
2. Find plugins in the left palette by category
3. Drag onto canvas to add to pipeline
4. Configure in the right panel
5. Connect with edges

## Common Pipeline Patterns

### Simple RAG
```
[Retrieval] → [Generation]
```

### Cached RAG
```
[Cache] → [Retrieval] → [Generation]
```

### Routed RAG
```
[Router] ─┬─→ [Rules Retrieval] ─┬─→ [Generation]
          └─→ [Strategy Retrieval]─┘
```

### Full CRAG
```
[Cache] → [Router] → [Retrieval] → [Evaluation] ─┬─→ [Generation] → [Validation]
                                                 └─→ [Rewrite] ───↩
```

## Creating Custom Plugins

See the [Plugin Development Guide](../plugin-development-guide.md) for creating your own plugins.
