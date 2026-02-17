# Cross-Encoder Reranking

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 3,250 |
| **Cost/Query** | $0.013 |
| **Accuracy** | +8% above naive |
| **Latency** | 500ms–1s |
| **Priority** | **P0** - BALANCED Tier |

---

## Architecture Diagram

```
Query Input
    ↓
[Retrieval] Fetch documents (10)
    ├─ 5,000 tokens
    ↓
[Cross-Encoder] ms-marco-MiniLM (NOT LLM!)
    ├─ Score each (query, doc) pair: 0-1 relevance
    └─ Sort by relevance score
    ↓
[Top-5 Selection] Keep highest-scoring
    ├─ 2,500 tokens (from 5,000)
    ↓
[Generation]
    ├─ Input: 2,950 tokens
    └─ Output: 300 tokens
    ↓
Answer
```

---

## How It Works

1. **Retrieve**: Standard top-10 chunks
2. **Cross-Encoder Reranking**:
   - Model: ms-marco-MiniLM-L-6-v2
   - Input: (query, document_text) pairs
   - Output: Relevance score 0-1
   - **Cost**: ~$0.001 per query (separate model)
3. **Rerank**: Select top-5 highest-scoring
4. **Generate**: Synthesize from top-5

**Key Advantage**: 75x cheaper than LLM-based reranking!

---

## Token Breakdown

**Retrieval**:
- 10 chunks: 5,000 tokens

**Cross-Encoder** (separate model, not LLM):
- 0 LLM tokens
- Model inference: ~$0.001 cost

**Generation**:
- Input: 400 + 50 + 2,500 (top-5) = 2,950 tokens
- Output: 300 tokens

**Total**: 3,250 tokens

**Cost**: LLM $0.012 + Cross-Encoder $0.001 = **$0.013 per query**

---

## When to Use

✅ **Best For**:
- BALANCED tier (standard reranking)
- Cost-efficiency important
- 500ms-1s latency acceptable

❌ **Not For**:
- <100ms latency required
- Offline-only (requires model)

---

## Code Example

```python
class CrossEncoderReranker:
    def __init__(self):
        # Use existing model from sentence-transformers
        self.model = CrossEncoder(
            "cross-encoder/ms-marco-MiniLM-L-6-v2"
        )

    async def rerank_documents(
        self,
        query: str,
        documents: list[Document],
        top_k: int = 5
    ) -> list[Document]:
        """Rerank docs using cross-encoder"""

        # Prepare pairs: [(query, doc_text), ...]
        pairs = [
            [query, doc.content]
            for doc in documents
        ]

        # Score all pairs
        scores = self.model.predict(pairs)

        # Sort by score (descending)
        ranked = sorted(
            zip(documents, scores),
            key=lambda x: x[1],
            reverse=True
        )

        # Return top-k
        return [doc for doc, _ in ranked[:top_k]]

    async def rerank_in_pipeline(
        self,
        query: str,
        retrieved_docs: list[Document]
    ) -> list[Document]:
        """Integration with RAG pipeline"""

        # Step 1: Retrieve (done upstream)
        # Step 2: Rerank
        reranked = await self.rerank_documents(
            query,
            retrieved_docs,
            top_k=5
        )

        # Step 3: Generate (done downstream)
        return reranked
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Cost** | $0.001/query (vs $0.033 LLM-based) | 75x cheaper! | Requires model inference |
| **Accuracy** | +8% improvement | Simpler than LLM-based |
| **Latency** | 50-100ms overhead | Fast enough for BALANCED |
| **Implementation** | Well-documented | Straightforward setup |

---

## Integration

**Tier Level**: BALANCED tier (standard choice)

**Model Options**:
- ms-marco-MiniLM-L-6-v2: Fast, good accuracy
- ms-marco-MiniLM-L-12-v2: Slower, better accuracy
- mmarco-mMiniLMv2-L12-H384-v1: Multilingual

**Comparison**:
- vs LLM-based reranking: 75x cheaper, similar accuracy
- vs ColBERT: Faster (100ms vs 500ms), less accurate
- vs no reranking: +8% accuracy for minimal cost

---

## Research Sources

- [Reranking in RAG: Boosting Accuracy](https://research.aimultiple.com/hybrid-rag/)
- [Production Retrievers: ColBERT, SPLADE](https://machine-mind-ml.medium.com/production-rag-that-works-hybrid-search-re-ranking-colbert-splade-e5-bge-624e9703fa2b)
- [NVIDIA Reranking Guide](https://developer.nvidia.com/blog/enhancing-rag-pipelines-with-re-ranking/)

---

**Status**: Production-Ready | **MeepleAI Tier**: P0 BALANCED
