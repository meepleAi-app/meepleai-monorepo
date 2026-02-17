# Query Expansion

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 4,110 |
| **Cost/Query** | $0.016 |
| **Accuracy** | +7% above naive |
| **Latency** | 500ms–1s |
| **Priority** | **P2** - Recall |

---

## Architecture Diagram

```
Query Input: "pieces"
    ↓
[Expansion] Add synonyms/related terms
    ├─ Original: "pieces"
    ├─ Expand: "pieces, meeples, tokens, game pieces, figures"
    └─ Cost: 250 input + 80 output = 330 tokens
    ↓
[Retrieval] Single expanded query
    ├─ Higher recall (catches variations)
    └─ 3,000 tokens (6 chunks, better results)
    ↓
[Generation] Synthesize
    ├─ Input: 400 + 80 (expanded) + 3,000 = 3,480 tokens
    └─ Output: 300 tokens
    ↓
Answer (catches terminology variations)
```

---

## How It Works

1. **Query Expansion** (LLM):
   - Add synonyms and related terms
   - Expand search space semantically
   - Cost: 250 input + 80 output = 330 tokens

2. **Enhanced Retrieval**:
   - Expanded query helps find more variations
   - Better recall (fewer false negatives)
   - Cost: 3,000 tokens (6 chunks vs 2.5K standard)

3. **Generation**:
   - Synthesize from expanded results
   - Cost: 3,480 input + 300 output = 3,780 tokens

---

## Token Breakdown

**Expansion**:
- Input: 200 (prompt) + 50 (query) = 250 tokens
- Output: 80 tokens (expanded query)

**Retrieval**:
- Expanded query: 3,000 tokens (6 chunks, higher hit rate)

**Generation**:
- Input: 400 + 80 + 3,000 = 3,480 tokens
- Output: 300 tokens

**Total**: 250 + 80 + 3,000 + 3,480 + 300 = **4,110 tokens**

---

## When to Use

✅ **Best For**:
- Terminology mismatches ("pieces" vs "meeples")
- Synonym variations
- When recall important

❌ **Not For**:
- Exact term matching
- Latency-critical (<500ms)

---

## Code Example

```python
class QueryExpansion:
    async def expand_and_retrieve(
        self,
        query: str
    ) -> list[Document]:
        """Expand query with synonyms and retrieve"""

        # Step 1: Expand query
        expanded = await self.expand_query(query)

        # Step 2: Retrieve with expanded query
        docs = await self.retrieve(expanded, top_k=6)

        return docs

    async def expand_query(self, query: str) -> str:
        """Expand query with synonyms/related terms"""

        prompt = f"""
        Expand this query by adding synonyms and related terms:
        {query}

        Return expanded query (single sentence, natural phrasing):
        """

        expanded = await self.llm.generate(prompt)
        return expanded

    async def retrieve(
        self,
        query: str,
        top_k: int = 6
    ) -> list[Document]:
        """Standard retrieval with expanded query"""

        query_emb = await self.embedder.encode(query)
        return await self.vector_db.search(query_emb, top_k=top_k)
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Recall** | +5-8% improvement | Retrieves more chunks (+500 tokens) |
| **Precision** | May retrieve irrelevant | Query expansion overhead |
| **Use Case** | Good for terminology variation | Standard queries unchanged |

---

## Integration

**Tier Level**: FAST/BALANCED tier (low cost, moderate benefit)

**Best Combined With**: Reranking (to filter expanded results)

---

## Research Sources

- [Query Expansion for RAG](https://haystack.deepset.ai/blog/query-expansion)

---

**Status**: Production-Ready | **MeepleAI Tier**: P2 Recall
