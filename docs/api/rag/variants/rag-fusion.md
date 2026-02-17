# RAG-Fusion

**Stats**: 11,550 tokens/query | $0.041/query | +11% accuracy | 2-4s latency | **Priority: P2**

## Architecture

```
User Query: "How do resources work?"
    ↓
┌─────────────────────────────────────────┐
│ Query Reformulation (LLM)               │
│ Generate 3-5 perspectives:              │
│ 1. "What are the resource types?"       │
│ 2. "How do I gain resources?"           │
│ 3. "How are resources spent?"           │
└─────────────────────────────────────────┘
    ↓
Parallel Retrieval (3 queries × 5 chunks = 15 unique chunks)
    ↓
Reciprocal Rank Fusion (deduplicate → 10 chunks)
    ↓
Optional Reranking (cross-encoder)
    ↓
Generate with Top 5 Fused Chunks
```

## How It Works

RAG-Fusion generates multiple query reformulations from different perspectives, retrieves results for each variant in parallel, then fuses results using Reciprocal Rank Fusion (RRF) to identify the most relevant chunks across all perspectives.

The workflow: (1) LLM reformulates the original query into 3-5 variations emphasizing different aspects or perspectives, (2) each query variant is embedded and retrieves top-K chunks independently, (3) RRF algorithm merges all result sets by combining rank positions, (4) deduplication removes chunks appearing in multiple result sets (but they receive higher fusion scores), (5) top-N fused chunks are selected for generation.

For ambiguous queries like "How do resources work?", single-query retrieval might miss important aspects. RAG-Fusion's reformulations capture different angles: "resource types" (what), "gaining resources" (how to acquire), "spending resources" (how to use). This multi-perspective retrieval achieves 8-12% better recall than single-query approaches.

The token cost is high (5.8x naive RAG) because multiple retrievals return 15 chunks total, and even after deduplication, 10 chunks remain. Optional reranking further improves precision.

## Token Breakdown

**Query Reformulation**:
- Input: 300 (prompt) + 50 (original query) = 350 tokens
- Output: 150 tokens (3 reformulated queries @ 50 tokens each)

**Parallel Retrieval** (3 query variants):
- Query 1: 5 chunks × 500 = 2,500 tokens
- Query 2: 5 chunks × 500 = 2,500 tokens
- Query 3: 5 chunks × 500 = 2,500 tokens
- **Total retrieved**: 7,500 tokens

**Reciprocal Rank Fusion** (algorithm-based, 0 LLM tokens):
- Deduplicate overlapping chunks
- Result: 10 unique chunks = 5,000 tokens

**Optional Reranking** (LLM-based):
- Input: 200 (prompt) + 50 (query) + 5,000 (chunks) = 5,250 tokens
- Output: 50 tokens (rankings)
- **Skip for token efficiency** (use cross-encoder instead)

**Generation** (Top 5 after fusion/reranking):
- Input: 400 + 50 + 2,500 = 2,950 tokens
- Output: 300 tokens

**Total** (with LLM reranking): 350 + 150 + 5,250 + 50 + 2,950 + 300 = **11,550 tokens**
**Optimized** (skip LLM reranking): 350 + 150 + 2,950 + 300 = **3,750 tokens** (67% reduction)

## When to Use

- **Ambiguous queries** that could be interpreted multiple ways (user intent unclear)
- **Broad conceptual questions** requiring multi-faceted answers (e.g., "How does trading work?")
- **PRECISE tier** where comprehensive coverage justifies token costs (or use optimized version)

## Code Example

```python
from anthropic import Anthropic
from typing import List, Dict

client = Anthropic(api_key="...")

def rag_fusion(query: str, num_variants: int = 3) -> str:
    """RAG-Fusion with query reformulation and reciprocal rank fusion."""

    # Step 1: Generate query reformulations
    reformulation = client.messages.create(
        model="claude-3-5-haiku-20241022",
        max_tokens=150,
        messages=[{
            "role": "user",
            "content": f"""Generate {num_variants} reformulations of this query from different perspectives.

Original: {query}

Output as JSON array: ["variant 1", "variant 2", "variant 3"]
"""
        }]
    )

    import json
    query_variants = json.loads(reformulation.content[0].text)
    query_variants.append(query)  # Include original

    # Step 2: Parallel retrieval for all variants
    all_results = []
    for variant in query_variants:
        embedding = get_embedding(variant)
        chunks = vector_search(embedding, top_k=5)
        all_results.append({
            "query": variant,
            "chunks": chunks
        })

    # Step 3: Reciprocal Rank Fusion
    fused_chunks = reciprocal_rank_fusion(all_results, k=60)

    # Step 4: Select top 5 after fusion (skip LLM reranking for efficiency)
    top_chunks = fused_chunks[:5]
    context = "\n\n".join([c['text'] for c in top_chunks])

    # Step 5: Generate with fused context
    response = client.messages.create(
        model="claude-3-5-sonnet-20241022",
        max_tokens=300,
        messages=[{
            "role": "user",
            "content": f"Question: {query}\n\nContext:\n{context}\n\nAnswer:"
        }]
    )

    return response.content[0].text

def reciprocal_rank_fusion(results: List[Dict], k: int = 60) -> List[Dict]:
    """Merge results from multiple queries using RRF."""
    scores = {}
    chunks_map = {}

    for result in results:
        for rank, chunk in enumerate(result['chunks'], start=1):
            chunk_id = chunk['id']
            # RRF formula: score = 1 / (k + rank)
            scores[chunk_id] = scores.get(chunk_id, 0) + 1 / (k + rank)
            if chunk_id not in chunks_map:
                chunks_map[chunk_id] = chunk

    # Sort by fused score (descending)
    sorted_ids = sorted(scores.keys(), key=lambda x: scores[x], reverse=True)
    return [chunks_map[chunk_id] for chunk_id in sorted_ids]
```

## Integration

RAG-Fusion integrates as a **pre-retrieval enhancement** in TOMAC-RAG, generating multiple query variants before retrieval.

**Enhanced Flow**:
1. **Layer 1 (Routing)**: Detect ambiguous query (vague keywords, <5 words)
2. **Layer 2.5** (NEW): Query reformulation (THIS LAYER)
3. **Layer 3**: Parallel retrieval for all query variants
4. **Layer 3.5**: Reciprocal Rank Fusion + deduplication
5. **Layer 4**: CRAG evaluation on fused chunks
6. **Layer 5**: Generate with top 5 fused chunks
7. **Layer 6**: Validate answer

**Optimization Strategies**:
- **Reduce variants**: 3 instead of 5 → save ~30% tokens
- **Skip LLM reranking**: Use cross-encoder or skip reranking → save ~5,250 tokens
- **Optimized total**: ~4,200 tokens (63% reduction)

**Activation Criteria**:
- Query length: <5 words (too vague)
- Ambiguity keywords: "how", "what", "explain" (without specifics)
- User tier: Editor/Admin (or use optimized version for all tiers)

## Sources

- [RAG Variants Explained: RAG-Fusion](https://www.jellyfishtechnologies.com/rag-variants-explained-classic-rag-graph-rag-hyde-and-ragfusion/)
- [RAG-Fusion Repository](https://github.com/Raudaschl/RAG-Fusion)
