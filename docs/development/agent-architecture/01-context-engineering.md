# Context Engineering Framework

**From Document Retrieval to Dynamic Context Assembly**

> 📖 **Parent**: [Multi-Agent Architecture](./README.md)

## Overview

Context Engineering represents the 2025-2026 evolution of RAG systems. Instead of simply retrieving documents, it **dynamically assembles multi-source context** tailored to each agent's needs.

## Traditional RAG vs Context Engineering

### Traditional RAG (2023-2024)
```
User Query → Embed → Vector Search → Top-K Docs → LLM → Generate
```

**Limitations**: Single knowledge source, no conversation context, ignores dynamic state

### Context Engineering (2025-2026)
```
User Intent → Multi-Source Context Assembly → Contextual Retrieval → Agent Action
```

**Sources**:
1. Static Knowledge (game rules RAG)
2. Dynamic Memory (conversation history)
3. Agent State (current game board)
4. Tool Metadata (available actions)

**Advantages**: Multi-source integration, temporal relevance, state awareness

## Context Budget Management

Priority-based token allocation for 8K context window:

| Priority | Source | Tokens | Required |
|----------|--------|--------|----------|
| 1 | Game state | 500 | ✅ Yes |
| 2 | Top-3 rules | 1500 | ✅ Yes |
| 3 | Conversation | 1000 | ⚠️ Optional |
| 4 | Extended context | 5000 | ⚠️ Optional |

## Retrieval Strategies by Source

### Static Knowledge: Hybrid Search
**Pattern**: Keyword (BM25) + Semantic (vector) + Reranking (cross-encoder)

### Dynamic Memory: Temporal Scoring
**Pattern**: Semantic similarity (60%) + Recency boost (40%)

### Agent State: Position Similarity
**Pattern**: Embed game state → Similar position search

### Tool Metadata: Capability Matching
**Pattern**: Filter tools by applicability to current state

## Performance: 3-Tier Caching

```
Request → Memory Cache (μs) → Redis Cache (ms) → Qdrant (100ms+)
```

**Cache Hit Rate Target**: >80%

## Next Steps

- [Tutor Agent Implementation](./02-tutor-agent.md)
- [Integration Guide](./06-integration.md)
- [Testing Strategy](./07-testing.md)
