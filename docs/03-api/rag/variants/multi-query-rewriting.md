# Multi-Query Rewriting

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 6,250 |
| **Cost/Query** | $0.024 |
| **Accuracy** | +9% above naive |
| **Latency** | 1–3s |
| **Priority** | **P2** - Ambiguous |

---

## Architecture Diagram

```
Query Input
    ↓
[Query Rewriting] Generate perspectives
    ├─ Original: "How do combat rules work?"
    ├─ Perspective 1: "What are the combat mechanics?"
    ├─ Perspective 2: "How do you fight in game?"
    └─ Perspective 3: "What determines combat outcome?"
    ↓
[Parallel Retrieval] 3 queries × 5 chunks = 15 chunks
    ├─ Deduplication: ~10 unique chunks
    └─ Total: 6,000 tokens
    ↓
[Generation] Synthesize from diverse results
    ├─ Input: 400 + 50 + 5,000 = 5,450 tokens
    └─ Output: 300 tokens
    ↓
Answer (multiple perspectives covered)
```

---

## How It Works

1. **Query Rewriting** (LLM):
   - Generate 3-5 query variants from different angles
   - Cost: 350 input + 150 output = 500 tokens

2. **Parallel Retrieval**:
   - Retrieve for each rewritten query
   - 3 queries × 5 chunks = 15 raw chunks
   - Deduplicate: ~10 unique (60% overlap typical)
   - Cost: ~6,000 tokens

3. **Deduplication**:
   - Remove duplicates
   - Keep 10 unique chunks

4. **Generation**:
   - Synthesize comprehensive answer
   - Cover multiple perspectives

---

## Token Breakdown

**Query Rewriting**:
- Input: 300 + 50 = 350 tokens
- Output: 150 tokens (3 rewrites @ 50 each)

**Parallel Retrieval** (3 queries):
- 6,000 tokens (12 chunks, deduplicated to 10)

**Generation**:
- Input: 400 + 50 + 5,000 (10 deduplicated) = 5,450 tokens
- Output: 300 tokens

**Total**: 350 + 150 + 6,000 + 5,450 + 300 = **6,250 tokens**

---

## When to Use

✅ **Best For**:
- Ambiguous queries (multiple interpretations)
- Complex rules needing multiple angles
- When precision important

❌ **Not For**:
- Simple exact lookups
- Latency-critical (<1s)

---

## Code Example

```python
class MultiQueryRewriting:
    async def rewrite_and_retrieve(
        self,
        query: str
    ) -> list[Document]:
        """Rewrite query from multiple perspectives"""

        # Step 1: Generate query rewrites
        rewrites = await self.generate_rewrites(query)

        # Step 2: Parallel retrieval
        all_results = []
        results = await asyncio.gather(
            self.retrieve(query, top_k=5),
            self.retrieve(rewrites[0], top_k=5),
            self.retrieve(rewrites[1], top_k=5),
            self.retrieve(rewrites[2], top_k=5)
        )

        for result_set in results:
            all_results.extend(result_set)

        # Step 3: Deduplicate
        unique_docs = self.deduplicate(all_results)

        return unique_docs[:10]

    async def generate_rewrites(
        self,
        query: str
    ) -> list[str]:
        """Generate multiple query reformulations"""

        prompt = f"""
        Generate 3 different ways to ask this question,
        from different perspectives:
        {query}

        Return JSON: ["question1", "question2", "question3"]
        """

        response = await self.llm.generate(prompt)
        return json.loads(response)

    def deduplicate(
        self,
        documents: list[Document]
    ) -> list[Document]:
        """Remove duplicate documents"""

        seen_ids = set()
        unique = []

        for doc in documents:
            if doc.id not in seen_ids:
                seen_ids.add(doc.id)
                unique.append(doc)

        return unique
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Accuracy** | +9% (multiple perspectives) | 6,250 tokens (3x) |
| **Coverage** | Handles ambiguity well | Multiple retrieval calls |
| **Cost** | $0.024 (moderate) | Rewriting overhead |
| **Latency** | 1-3s (acceptable) | Parallel retrieval slower |

---

## Integration

**Tier Level**: BALANCED tier (ambiguous queries)

**Query Variant Strategy**:
- Original query
- Synonym/paraphrase
- Different angle/perspective
- Question format variant

---

## Research Sources

- [Query Transformations: Multi-Query](https://dev.to/jamesli/in-depth-understanding-of-rag-query-transformation-optimization-multi-query-problem-decomposition-and-step-back-27jg)
- [RAG Variants Explained: RAG-Fusion](https://www.jellyfishtechnologies.com/rag-variants-explained-classic-rag-graph-rag-hyde-and-ragfusion/)

---

**Status**: Production-Ready | **MeepleAI Tier**: P2 Ambiguous
