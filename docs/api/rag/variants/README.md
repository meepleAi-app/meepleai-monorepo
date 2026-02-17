# RAG Variants Documentation

Comprehensive documentation for 15 RAG variants implemented or planned for MeepleAI's TOMAC-RAG architecture.

## 6 Routing Strategies

TOMAC-RAG routes queries to one of 6 strategies, each combining multiple variants:

| Strategy | Tokens | Cost | Latency | Accuracy | Phases |
|----------|--------|------|---------|----------|--------|
| 🚀 **FAST** | 2,060 | $0.0001 | <200ms | 78-85% | Synthesis |
| ⚖️ **BALANCED** | 2,820 | $0.01 | 1-2s | 85-92% | Synthesis + CRAG |
| 🎯 **PRECISE** | 22,396 | $0.132 | 5-10s | 95-98% | Retrieval + Analysis + Synthesis + Validation |
| 🔬 **EXPERT** | 15,000 | $0.099 | 8-15s | 92-96% | Web Search + Multi-Hop + Synthesis |
| 🗳️ **CONSENSUS** | 18,000 | $0.09 | 10-20s | 97-99% | 3 Voters + Aggregator |
| ⚙️ **CUSTOM** | Variable | Variable | Variable | Variable | Admin configured |

> **Note**: Anonymous users cannot access the RAG system. Authentication is required.

## Priority P0 (Implemented/High Priority)

1. **[Semantic Cache](01-semantic-cache.md)** - 986 tokens | $0.004 | +0% accuracy | 50% token reduction
2. **[Contextual Embeddings](02-contextual-embeddings.md)** - 1,950 tokens | $0.007 | +5% accuracy | 30% reduction
3. **[Metadata Filtering](03-metadata-filtering.md)** - 3,100 tokens | $0.012 | +6% accuracy | Multi-game filtering
4. **[Cross-Encoder Reranking](04-cross-encoder-reranking.md)** - 3,250 tokens | $0.013 | +8% accuracy | BALANCED tier
5. **[Hybrid Search](05-hybrid-search.md)** - 4,250 tokens | $0.016 | +11% accuracy | Vector + BM25
6. **[Advanced RAG](06-advanced-rag.md)** - 3,700 tokens | $0.013 | +10% accuracy | Production foundation

## Priority P1 (High Value)

7. **[Sentence Window](sentence-window.md)** - 3,250 tokens | $0.012 | +7% accuracy | Granular retrieval
8. **[ColBERT Reranking](colbert-reranking.md)** - 3,250 tokens | $0.014 | +12% accuracy | Late interaction
9. **[Chain-of-Thought RAG](chain-of-thought-rag.md)** - 3,650 tokens | $0.016 | +18% accuracy | Reasoning transparency
10. **[Query Decomposition](query-decomposition.md)** - 6,550 tokens | $0.026 | +12% accuracy | Multi-concept queries
11. **[Iterative RAG](iterative-rag.md)** - 6,736 tokens | $0.025 | +14% accuracy | Feedback loops

## Priority P2 (Specialized Use Cases)

12. **[Multi-Agent RAG](multi-agent-rag.md)** - 12,900 tokens | $0.043 | +20% accuracy | PRECISE tier
13. **[RAG-Fusion](rag-fusion.md)** - 11,550 tokens | $0.041 | +11% accuracy | Query reformulation
14. **[Step-Back Prompting](step-back-prompting.md)** - 5,740 tokens | $0.022 | +10% accuracy | Conceptual grounding
15. **[Query Expansion](query-expansion.md)** - 4,110 tokens | $0.016 | +7% accuracy | Synonym matching

## Quick Reference

### By Token Efficiency

| Variant | Tokens | Cost | Priority |
|---------|--------|------|----------|
| Semantic Cache | 986 | $0.004 | P0 |
| Contextual Embeddings | 1,950 | $0.007 | P0 |
| Metadata Filtering | 3,100 | $0.012 | P0 |
| Sentence Window | 3,250 | $0.012 | P1 |
| Cross-Encoder Reranking | 3,250 | $0.013 | P0 |
| CoT-RAG | 3,650 | $0.016 | P1 |
| Advanced RAG | 3,700 | $0.013 | P0 |
| Query Expansion | 4,110 | $0.016 | P2 |
| Hybrid Search | 4,250 | $0.016 | P0 |
| Step-Back Prompting | 5,740 | $0.022 | P2 |
| Query Decomposition | 6,550 | $0.026 | P1 |
| Iterative RAG | 6,736 | $0.025 | P1 |
| RAG-Fusion | 11,550 | $0.041 | P2 |
| Multi-Agent RAG | 12,900 | $0.043 | P2 |

### By Accuracy Gain

| Variant | Accuracy Gain | Tokens | Priority |
|---------|---------------|--------|----------|
| Multi-Agent RAG | +20% | 12,900 | P2 |
| CoT-RAG | +18% | 3,650 | P1 |
| Iterative RAG | +14% | 6,736 | P1 |
| ColBERT Reranking | +12% | 3,250 | P1 |
| Query Decomposition | +12% | 6,550 | P1 |
| Hybrid Search | +11% | 4,250 | P0 |
| RAG-Fusion | +11% | 11,550 | P2 |
| Advanced RAG | +10% | 3,700 | P0 |
| Step-Back Prompting | +10% | 5,740 | P2 |
| Cross-Encoder Reranking | +8% | 3,250 | P0 |
| Sentence Window | +7% | 3,250 | P1 |
| Query Expansion | +7% | 4,110 | P2 |
| Metadata Filtering | +6% | 3,100 | P0 |
| Contextual Embeddings | +5% | 1,950 | P0 |
| Semantic Cache | +0% | 986 | P0 |

## Implementation Roadmap

### Phase 0: Quick Wins (Weeks 0-2)
- Semantic Cache (extend Redis)
- Metadata Filtering (add metadata to chunks)

### Phase 1: Foundation (Weeks 1-3)
- Contextual Embeddings (re-embed with context)
- Hybrid Search (vector + BM25)

### Phase 2: Quality (Weeks 4-7)
- Advanced RAG (CRAG + cross-encoder)
- Cross-Encoder Reranking (BALANCED tier)

### Phase 3: Advanced (Weeks 8-11)
- CoT-RAG (reasoning transparency)
- Iterative RAG (feedback loops)

### Phase 4: Specialized (Weeks 12+)
- Multi-Agent RAG (PRECISE tier)
- Remaining P2 variants as needed

## Integration with TOMAC-RAG

All variants integrate into the 6-layer TOMAC-RAG architecture:

1. **Layer 1 (Routing)**: Selects appropriate variant based on query complexity
2. **Layer 2 (Caching)**: Semantic cache intercepts before expensive retrieval
3. **Layer 3 (Retrieval)**: Hybrid search, metadata filtering, sentence window
4. **Layer 4 (CRAG)**: Correctness evaluation for all variants
5. **Layer 5 (Generation)**: CoT-RAG, multi-agent synthesis
6. **Layer 6 (Validation)**: Quality checks for all outputs

## Sources

All documentation sourced from comprehensive RAG research catalog:
- `claudedocs/rag_complete_variants_token_costs.md`
- Academic papers, industry blogs, production case studies
- Token analysis and cost modeling

---

**Last Updated**: 2026-01-31
**Total Variants**: 15
**Coverage**: P0 (6), P1 (5), P2 (4)
