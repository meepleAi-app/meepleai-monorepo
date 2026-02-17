# Hybrid Search RAG

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 4,250 |
| **Cost/Query** | $0.016 |
| **Accuracy** | +11% above naive |
| **Latency** | 500ms–1s |
| **Priority** | **P0** - BALANCED Tier |

---

## Architecture Diagram

```
Query Input
    ↓
[Parallel Search]
    ├─ Vector Search (semantic): top-5 chunks
    ├─ BM25 Search (keyword): top-5 chunks
    └─ Cost: 5,000 tokens
    ↓
[Reciprocal Rank Fusion] Merge & deduplicate
    ├─ Combine scores: vector + keyword
    └─ Result: 7 unique chunks (3,500 tokens)
    ↓
[Generation]
    ├─ Input: 400 + 50 + 3,500 = 3,950 tokens
    └─ Output: 300 tokens
    ↓
Answer (best of both worlds)
```

---

## How It Works

1. **Vector Search**: Find semantically similar chunks
   - Captures meaning
   - Handles synonyms
2. **BM25 Search**: Find exact keyword matches
   - Precision on exact terms
   - Handles specific rule names
3. **Reciprocal Rank Fusion**: Merge results
   - Combine vector + keyword scores
   - Deduplicate overlapping results
4. **Generation**: Synthesize from hybrid results

---

## Token Breakdown

**Parallel Retrieval**:
- Vector: 2,500 tokens (5 chunks)
- BM25: 2,500 tokens (5 chunks, may overlap)
- After RRF deduplication: 3,500 tokens (7 unique chunks)

**Generation**:
- Input: 400 + 50 + 3,500 = 3,950 tokens
- Output: 300 tokens

**Total**: 4,250 tokens

**Cost**: $0.016 per query

---

## When to Use

✅ **Best For**:
- Mixed query types (semantic + keyword)
- Multi-game knowledge (exact term matching + concept matching)
- BALANCED tier standard

✅ **Advantages**:
- Semantic: "What happens with food?" → semantic similarity
- Keyword: "setup phase" → exact match
- Hybrid: Best of both!

---

## Code Example

```python
class HybridSearch:
    async def hybrid_search(
        self,
        query: str,
        top_k: int = 5
    ) -> list[Document]:
        """Vector + BM25 search with fusion"""

        # Parallel vector and keyword search
        vector_results, bm25_results = await asyncio.gather(
            self.vector_search(query, top_k=top_k),
            self.bm25_search(query, top_k=top_k)
        )

        # Reciprocal rank fusion
        fused = self.reciprocal_rank_fusion(
            vector_results,
            bm25_results
        )

        return fused[:top_k]

    async def vector_search(
        self,
        query: str,
        top_k: int = 5
    ) -> list[tuple[Document, float]]:
        """Semantic search using embeddings"""

        query_emb = await self.embedder.encode(query)
        results = await self.qdrant.search(
            query_emb,
            limit=top_k
        )

        return [
            (doc, score)
            for doc, score in results
        ]

    async def bm25_search(
        self,
        query: str,
        top_k: int = 5
    ) -> list[tuple[Document, float]]:
        """Keyword search using BM25"""

        # BM25 from PostgreSQL or Elasticsearch
        results = await self.bm25_index.search(
            query,
            limit=top_k
        )

        return [
            (doc, score)
            for doc, score in results
        ]

    def reciprocal_rank_fusion(
        self,
        vector_results: list[tuple[Document, float]],
        bm25_results: list[tuple[Document, float]],
        k: int = 60
    ) -> list[Document]:
        """Merge using reciprocal rank fusion"""

        scores = {}

        # Vector scores: RRF formula = 1 / (k + rank)
        for rank, (doc, _) in enumerate(vector_results):
            scores[doc.id] = scores.get(doc.id, 0) + 1 / (k + rank)

        # BM25 scores
        for rank, (doc, _) in enumerate(bm25_results):
            scores[doc.id] = scores.get(doc.id, 0) + 1 / (k + rank)

        # Sort by fused score
        sorted_docs = sorted(
            scores.items(),
            key=lambda x: x[1],
            reverse=True
        )

        return [self.doc_id_map[doc_id] for doc_id, _ in sorted_docs]
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Accuracy** | +11% (semantic + keyword) | Dual search overhead |
| **Coverage** | Best of both worlds | 5,000 tokens raw (deduplicated) |
| **Latency** | 500ms-1s (parallel search) | Two models needed |
| **Cost** | $0.016 (moderate) | Worth it for accuracy |

---

## Integration

**Tier Level**: BALANCED tier (standard choice)

**Technology**:
- Vector: Qdrant
- Keyword: BM25 (PostgreSQL, Elasticsearch)
- Fusion: RRF algorithm (no additional cost)

---

## Research Sources

- [Hybrid RAG: Boosting RAG Accuracy](https://research.aimultiple.com/hybrid-rag/)
- [Contextual RAG Systems with Hybrid Search and Reranking](https://www.analyticsvidhya.com/blog/2024/12/contextual-rag-systems-with-hybrid-search-and-reranking/)

---

**Status**: Production-Ready | **MeepleAI Tier**: P0 BALANCED
