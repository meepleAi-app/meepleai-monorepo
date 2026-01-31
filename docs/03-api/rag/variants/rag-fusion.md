# RAG-Fusion

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 11,550 |
| **Cost/Query** | $0.041 |
| **Accuracy** | +11% above naive |
| **Latency** | 2–4s |
| **Priority** | **P2** - Ensemble |

---

## Architecture Diagram

```
Query Input
    ↓
[Query Reformulation] Generate 5 variants
    ├─ Input: 300 + 50 = 350 tokens
    └─ Output: 150 tokens (5 questions @ 30 each)
    ↓
[Parallel Retrieval] 5 queries × 5 chunks
    ├─ 25 raw chunks
    └─ After deduplication: ~15 chunks (5,000 tokens after RRF)
    ↓
[Reciprocal Rank Fusion] Merge without LLM
    ├─ Algorithm: RRF = 1/(k + rank)
    └─ No token cost (pure algorithm)
    ↓
[Reranking] (Optional)
    ├─ Cross-encoder: 7,500 tokens
    └─ Or skip for faster execution
    ↓
[Generation]
    ├─ Input: 400 + 50 + 2,500 (top-5) = 2,950 tokens
    └─ Output: 300 tokens
    ↓
Answer (ensemble perspective)
```

---

## How It Works

1. **Query Reformulation**:
   - Generate 5 variants of the question
   - Cost: 350 input + 150 output = 500 tokens

2. **Parallel Retrieval**:
   - Retrieve top-5 for each of 5 queries
   - 25 raw chunks, deduplicate to ~15 unique
   - Cost: 7,500 tokens

3. **Reciprocal Rank Fusion** (No LLM Cost):
   - Merge rankings algorithmically
   - Formula: RRF = Σ 1/(k + rank_i)
   - Produces fused rankings

4. **Optional Reranking**:
   - Cross-encoder to filter: 7,500 tokens
   - Or skip for speed

5. **Generation**:
   - Synthesize from top-5 fused results
   - Cost: 3,250 tokens

---

## Token Breakdown

**Query Reformulation**: 500 tokens

**Parallel Retrieval**: 7,500 tokens (25 chunks, RRF to 15)

**RRF Fusion**: 0 tokens (algorithm only)

**Reranking** (if included): 7,500 tokens

**Generation**: 3,250 tokens

**Total (with reranking)**: 500 + 7,500 + 7,500 + 3,250 = **19,250 tokens**
**Total (without reranking)**: 500 + 7,500 + 3,250 = **11,250 tokens**

---

## When to Use

✅ **Best For**:
- Complex multi-faceted queries
- When ensemble benefits sought
- Higher accuracy needed

❌ **Not For**:
- Budget-conscious (19K tokens, $0.041)
- Latency-critical (2-4s)

---

## Code Example

```python
class RagFusion:
    async def execute_fusion(
        self,
        query: str
    ) -> list[Document]:
        """RAG-Fusion: reformulation + RRF"""

        # Step 1: Generate variants
        variants = await self.reformulate(query)

        # Step 2: Parallel retrieval
        all_results = await asyncio.gather(
            *[self.retrieve(v, top_k=5) for v in variants]
        )

        # Step 3: RRF fusion
        fused = self.reciprocal_rank_fusion(all_results)

        # Step 4: Optional reranking
        reranked = await self.rerank(query, fused)

        return reranked[:5]

    async def reformulate(self, query: str) -> list[str]:
        """Generate multiple query variants"""

        prompt = f"""
        Generate 5 different phrasings of this question:
        {query}

        Return JSON: ["q1", "q2", ...]
        """

        response = await self.llm.generate(prompt)
        return json.loads(response)

    def reciprocal_rank_fusion(
        self,
        result_sets: list[list[tuple[Document, float]]],
        k: int = 60
    ) -> list[Document]:
        """Fuse rankings using RRF"""

        scores = {}

        for results in result_sets:
            for rank, (doc, _) in enumerate(results):
                # RRF formula
                rrf_score = 1.0 / (k + rank + 1)
                if doc.id in scores:
                    scores[doc.id] += rrf_score
                else:
                    scores[doc.id] = rrf_score

        # Sort by RRF score
        sorted_docs = sorted(
            scores.items(),
            key=lambda x: x[1],
            reverse=True
        )

        return [self.doc_id_map[doc_id] for doc_id, _ in sorted_docs]

    async def rerank(
        self,
        query: str,
        docs: list[Document]
    ) -> list[Document]:
        """Optional: Rerank with cross-encoder"""

        # Cross-encoder reranking
        scores = await self.cross_encoder.rank(query, docs)

        # Sort by score
        ranked = sorted(
            zip(docs, scores),
            key=lambda x: x[1],
            reverse=True
        )

        return [doc for doc, _ in ranked]
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Accuracy** | +11% (ensemble benefit) | 11,550 tokens (5-6x) |
| **Cost** | $0.041 (expensive) | Multiple reformulations |
| **Latency** | 2-4s (slower) | 5 parallel retrievals |

---

## Integration

**Tier Level**: BALANCED/PRECISE tier (ensemble scenarios)

**Optimization**:
- Skip reranking for speed: 11,250 tokens
- Use 3 variants instead of 5: 8,500 tokens
- Combine with caching: pre-generate variants for common queries

---

## Research Sources

- [RAG-Fusion for Ensemble Retrieval](https://www.jellyfishtechnologies.com/rag-variants-explained-classic-rag-graph-rag-hyde-and-ragfusion/)

---

**Status**: Production-Ready | **MeepleAI Tier**: P2 Ensemble
