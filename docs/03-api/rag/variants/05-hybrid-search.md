# Hybrid Search RAG

**Stats**: 4,250 tokens/query | $0.016/query | +11% accuracy | 500ms-1s latency | **Priority: P0**

## Architecture

```
User Query
    ↓
┌─────────────────────────────────────────┐
│ Parallel Retrieval                      │
├─────────────────────────────────────────┤
│ Vector Search (semantic)    │ 5 chunks  │
│ BM25 Keyword (lexical)      │ 5 chunks  │
└─────────────────────────────────────────┘
    ↓
Reciprocal Rank Fusion (RRF)
    ↓
Deduplicated: 7 unique chunks (overlap removed)
    ↓
Generate (3,950 tokens)
```

## How It Works

Hybrid search combines vector similarity (semantic understanding) with keyword matching (lexical precision) to achieve best-of-both-worlds retrieval. Vector embeddings excel at conceptual matches ("resources" finds "food tokens") but struggle with exact terms, while BM25 excels at exact matches ("food tokens" finds "food tokens") but misses synonyms.

The system performs both searches in parallel: (1) vector search finds 5 semantically similar chunks, (2) BM25 finds 5 keyword-matching chunks. Reciprocal Rank Fusion (RRF) merges results by combining rank positions from both retrievers, then deduplicates overlapping chunks. A chunk appearing in both result sets receives a higher fusion score than chunks from only one retriever.

For board game rules, hybrid search is particularly valuable: exact terms like "food tokens" (game-specific terminology) benefit from keyword matching, while conceptual queries like "how do I get resources" benefit from semantic search. The combination achieves 10-12% better accuracy than either approach alone.

The token cost is moderate (4,250 tokens) because both retrievers may return some identical chunks, and deduplication reduces the final context size to ~7 chunks instead of 10.

## Token Breakdown

**Parallel Retrieval**:
- Vector search: 5 chunks × 500 = 2,500 tokens
- BM25 search: 5 chunks × 500 = 2,500 tokens
- Total candidates: 5,000 tokens

**Reciprocal Rank Fusion** (algorithm-based, 0 tokens):
- Deduplicate overlapping chunks
- Final result: 7 unique chunks = 3,500 tokens

**Generation**:
- Input: 400 + 50 + 3,500 = 3,950 tokens
- Output: 300 tokens

**Total**: **4,250 tokens**

## When to Use

- **BALANCED tier** in TOMAC-RAG (default strategy for production)
- **Board game rules** with mix of exact terminology and conceptual queries
- **Multi-language content** where BM25 handles exact foreign terms better than embeddings

## Code Example

```python
from anthropic import Anthropic
from qdrant_client import QdrantClient
from rank_bm25 import BM25Okapi
from typing import List, Dict

client = Anthropic(api_key="...")
qdrant = QdrantClient(host="localhost", port=6333)

def hybrid_search_rag(query: str) -> str:
    """RAG with hybrid search: vector + BM25 keyword fusion."""

    # Vector search (semantic)
    query_embedding = get_embedding(query)
    vector_results = qdrant.search(
        collection_name="rulebooks",
        query_vector=query_embedding,
        limit=5
    )
    vector_chunks = [
        {"id": hit.id, "text": hit.payload['text'], "vector_rank": i+1}
        for i, hit in enumerate(vector_results)
    ]

    # BM25 keyword search (lexical)
    bm25_results = bm25_search(query, top_k=5)
    bm25_chunks = [
        {"id": chunk['id'], "text": chunk['text'], "bm25_rank": i+1}
        for i, chunk in enumerate(bm25_results)
    ]

    # Reciprocal Rank Fusion (RRF)
    fused_chunks = reciprocal_rank_fusion(
        vector_chunks,
        bm25_chunks,
        k=60  # RRF parameter
    )

    # Take top 7 after fusion
    top_chunks = fused_chunks[:7]
    context = "\n\n".join([c['text'] for c in top_chunks])

    # Generate
    response = client.messages.create(
        model="claude-3-5-sonnet-20241022",
        max_tokens=300,
        messages=[{
            "role": "user",
            "content": f"Question: {query}\n\nContext:\n{context}\n\nAnswer:"
        }]
    )

    return response.content[0].text

def reciprocal_rank_fusion(
    vector_results: List[Dict],
    bm25_results: List[Dict],
    k: int = 60
) -> List[Dict]:
    """Merge results using Reciprocal Rank Fusion."""
    scores = {}
    chunks = {}

    # Score from vector search
    for chunk in vector_results:
        chunk_id = chunk['id']
        scores[chunk_id] = scores.get(chunk_id, 0) + 1 / (k + chunk['vector_rank'])
        chunks[chunk_id] = chunk

    # Score from BM25 search
    for chunk in bm25_results:
        chunk_id = chunk['id']
        scores[chunk_id] = scores.get(chunk_id, 0) + 1 / (k + chunk['bm25_rank'])
        if chunk_id not in chunks:
            chunks[chunk_id] = chunk

    # Sort by fused score
    sorted_ids = sorted(scores.keys(), key=lambda x: scores[x], reverse=True)
    return [chunks[chunk_id] for chunk_id in sorted_ids]
```

## Integration

Hybrid search integrates at **Layer 3 (Retrieval)** in TOMAC-RAG, replacing single-mode vector search with dual-mode fusion.

**Architecture Changes**:
1. **Layer 3A**: Vector search (semantic) → 5 chunks
2. **Layer 3B**: BM25 search (keyword) → 5 chunks (parallel execution)
3. **Layer 3C**: RRF fusion → 7 deduplicated chunks
4. Proceed to Layer 4 (CRAG evaluation) with fused chunks

**Infrastructure Requirements**:
- **Vector DB**: Qdrant (existing)
- **BM25 Index**: Elasticsearch or in-memory BM25 (new)
- **Indexing**: Dual-index all chunks at ingestion time

**Performance**: Parallel searches complete in ~400ms total (not sequential), minimal latency overhead vs single vector search.

## Sources

- [Hybrid RAG: Boosting Accuracy](https://research.aimultiple.com/hybrid-rag/)
- [Hybrid Search with Reciprocal Rank Fusion](https://www.elastic.co/blog/improving-information-retrieval-elastic-stack-hybrid)
