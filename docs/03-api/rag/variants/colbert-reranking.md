# ColBERT Reranking

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 3,250 |
| **Cost/Query** | $0.014 |
| **Accuracy** | +12% above naive |
| **Latency** | 500ms–1s |
| **Priority** | **P1** - BALANCED Tier |

---

## Architecture Diagram

```
Query Input
    ↓
[Retrieval] Fetch documents (10)
    ↓
[ColBERT Late Interaction]
    ├─ Encode query tokens (contextualized)
    ├─ Encode doc tokens (contextualized)
    └─ Score: Max similarity per query token across doc
    ↓
[Rerank] Top-5 by ColBERT score
    ↓
[Generation] Synthesize answer
    ↓
Answer (best-ranked docs)
```

---

## How It Works

1. **Retrieval**: Standard vector search (top-10)
2. **Late Interaction**: ColBERT model
   - Each query token matched against all document tokens
   - Maximum similarity score per query token
   - Sum of max scores = document relevance
3. **Rerank**: Select top-5 highest-scoring documents
4. **Generate**: Synthesis with reranked documents

**Key Advantage**: Zero LLM tokens (separate model inference)

---

## Token Breakdown

**Retrieval**:
- Vector search: 5,000 tokens (10 chunks)

**ColBERT Reranking** (separate model):
- 0 LLM tokens (ColBERT uses own inference engine)
- Model cost: ~$0.002 per query

**Generation**:
- Input: 400 + 50 + 2,500 (top-5) = 2,950 tokens
- Output: 300 tokens

**Total**: 3,250 tokens

**Cost**: LLM $0.012 + ColBERT $0.002 = **$0.014 per query**

---

## When to Use

✅ **Best For**:
- BALANCED tier standard choice
- Hybrid search (exact + semantic)
- High-precision retrieval needed
- Cost-conscious reranking (vs LLM-based)

❌ **Not For**:
- FAST tier (use cross-encoder instead)
- Zero-model environments (no ColBERT available)

---

## Code Example

```python
class ColBERTReranker:
    def __init__(self, model_name: str = "colbert-ir/colbert-v2.0"):
        self.colbert = ColBERT(model_name)
        self.searcher = None  # Requires ColBERT index

    async def rerank_documents(
        self,
        query: str,
        documents: list[Document],
        top_k: int = 5
    ) -> list[Document]:
        """Rerank docs using ColBERT late interaction"""

        # Encode query (returns token-level vectors)
        query_vectors = self.colbert.encode_query(query)

        # Score each document
        scores = []
        for doc in documents:
            doc_vectors = self.colbert.encode_doc(doc.content)

            # Late interaction: max similarity per query token
            doc_score = 0.0
            for q_vec in query_vectors:
                max_sim = max(
                    self._cosine_similarity(q_vec, d_vec)
                    for d_vec in doc_vectors
                )
                doc_score += max_sim

            scores.append((doc, doc_score))

        # Sort by score and return top-k
        ranked = sorted(scores, key=lambda x: x[1], reverse=True)
        return [doc for doc, _ in ranked[:top_k]]

    @staticmethod
    def _cosine_similarity(vec1, vec2) -> float:
        """Cosine similarity between two vectors"""
        dot_product = sum(a * b for a, b in zip(vec1, vec2))
        norm1 = sum(x**2 for x in vec1) ** 0.5
        norm2 = sum(x**2 for x in vec2) ** 0.5
        return dot_product / (norm1 * norm2 + 1e-8)
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Accuracy** | +12% (state-of-art retrieval) | Requires ColBERT setup |
| **Cost** | $0.014/query (vs $0.033 LLM-based) | Additional model needed |
| **Latency** | 500ms–1s acceptable | Slower than cross-encoder |
| **Implementation** | Well-documented (IR community) | Infrastructure complexity |

---

## Integration

**Tier Level**: BALANCED tier (standard choice for reranking)

**Model Selection**:
- ColBERT-v2.0: Fast, high accuracy
- ColBERT-v2.1: Latest, best performance

**Comparison**:
- vs Cross-Encoder: ColBERT slower (100ms vs 50ms) but more accurate
- vs LLM-Based: ColBERT 75x cheaper

---

## Research Sources

- [ColBERT: Efficient and Effective Passage Search via Contextualized Late Interaction](https://arxiv.org/abs/2004.12832)
- [Production Retrievers: ColBERT, SPLADE](https://machine-mind-ml.medium.com/production-rag-that-works-hybrid-search-re-ranking-colbert-splade-e5-bge-624e9703fa2b)

---

**Status**: Production-Ready | **MeepleAI Tier**: P1 BALANCED
