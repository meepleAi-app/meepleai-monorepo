# Query Decomposition

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 6,550 |
| **Cost/Query** | $0.026 |
| **Accuracy** | +12% above naive |
| **Latency** | 2–4s |
| **Priority** | **P1** - Complex |

---

## Architecture Diagram

```
Query Input
    ↓
[Decomposition] Break into sub-queries
    ├─ Original: "How do setup AND combat work together?"
    ├─ Sub-query 1: "What is the setup phase?"
    ├─ Sub-query 2: "What are combat rules?"
    └─ Sub-query 3: "How do they interact?"
    ↓
[Parallel Retrieval] For each sub-query
    ├─ 3 sub-queries × 5 chunks = 15 chunks
    └─ Deduplicated: ~10 unique chunks
    ↓
[Synthesis] Combine answers
    ├─ Input: 500 (synthesis prompt) + 150 (sub-queries) + 5,000 (docs)
    └─ Output: 400 tokens
    ↓
Answer (comprehensive multi-concept)
```

---

## How It Works

1. **Decompose Query**:
   - LLM breaks complex query into 3-5 sub-queries
   - Each sub-query targets one concept
   - Cost: 350 input + 150 output = 500 tokens

2. **Retrieve for Each Sub-query**:
   - Parallel retrieval (5 chunks each)
   - Some overlap, deduplicate to ~10 unique
   - Cost: ~5,000 tokens

3. **Synthesize**:
   - Combine answers from all sub-queries
   - Show relationships between concepts
   - Cost: 500 input + 400 output = 900 tokens

---

## Token Breakdown

**Decomposition**:
- Input: 300 + 50 = 350 tokens
- Output: 150 tokens (3 sub-queries)

**Retrieval** (3 × 5 = 15 raw chunks):
- Deduplicated to ~10: 5,000 tokens

**Synthesis**:
- Input: 500 + 150 + 5,000 = 5,650 tokens
- Output: 400 tokens

**Total**: 350 + 150 + 5,000 + 5,650 + 400 = **6,550 tokens**

---

## When to Use

✅ **Best For**:
- Multi-concept queries ("How do X AND Y interact?")
- Complex rule synthesis
- When relationships matter

❌ **Not For**:
- Simple single-concept queries
- Latency-critical

---

## Code Example

```python
class QueryDecomposition:
    async def decompose_and_retrieve(
        self,
        query: str
    ) -> list[Document]:
        """Decompose query and retrieve for each sub-query"""

        # Step 1: Decompose
        sub_queries = await self.decompose(query)

        # Step 2: Parallel retrieval
        all_docs = []
        results = await asyncio.gather(
            *[self.retrieve(sq, top_k=5) for sq in sub_queries]
        )

        for result_set in results:
            all_docs.extend(result_set)

        # Step 3: Deduplicate
        unique_docs = self.deduplicate(all_docs)

        return unique_docs[:10]

    async def decompose(self, query: str) -> list[str]:
        """Break query into sub-queries"""

        prompt = f"""
        Break this complex question into 3-5 simpler sub-questions:
        {query}

        Return JSON: ["sub_question_1", "sub_question_2", ...]
        """

        response = await self.llm.generate(prompt)
        return json.loads(response)

    async def synthesize_decomposed(
        self,
        query: str,
        sub_queries: list[str],
        docs: list[Document]
    ) -> str:
        """Synthesize answers from sub-queries"""

        prompt = f"""
        These sub-questions help answer the main question:
        {query}

        Sub-questions:
        {chr(10).join(f'- {sq}' for sq in sub_queries)}

        Documents:
        {self.format_docs(docs)}

        Synthesize a comprehensive answer showing how the concepts relate.
        """

        return await self.llm.generate(prompt)
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Accuracy** | +12% (multi-concept) | 6,550 tokens (3.3x) |
| **Coverage** | Handles complex queries | Decomposition overhead |
| **Cost** | $0.026 (moderate) | Multiple retrieval calls |

---

## Integration

**Tier Level**: BALANCED tier (complex queries)

---

## Research Sources

- [Query Decomposition & Multi-Concept](https://dev.to/jamesli/in-depth-understanding-of-rag-query-transformation-optimization-multi-query-problem-decomposition-and-step-back-27jg)

---

**Status**: Production-Ready | **MeepleAI Tier**: P1 Complex
