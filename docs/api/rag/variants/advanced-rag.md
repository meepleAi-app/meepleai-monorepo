# Advanced RAG

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 3,700–9,000 |
| **Cost/Query** | $0.013–$0.032 |
| **Accuracy** | +10–12% above naive |
| **Latency** | 500ms–2s |
| **Priority** | **P0** - Foundation |

---

## Architecture Diagram

```
Query Input
    ↓
[Pre-Retrieval] Query Rewriting (LLM)
    ├─ Paraphrase: "What is the setup rule?"
    ├─ Expand: Add synonyms
    └─ Output: 3-5 reformulated queries
    ↓
[Retrieval] Fetch Documents
    ├─ Retrieve top-10 chunks
    └─ Merge deduplication
    ↓
[Post-Retrieval] Reranking
    ├─ Cross-encoder reranking
    └─ Select top-5
    ↓
[Generation] Synthesize Answer
    ├─ Input: System prompt + Query + Top-5 docs
    └─ Output: Comprehensive answer
    ↓
Answer (citations required)
```

---

## How It Works

1. **Query Rewriting**: LLM reformulates original query from multiple perspectives
   - Synonym expansion: "setup" → "initialization"
   - Paraphrasing: Different phrasings of same intent
2. **Enhanced Retrieval**: Retrieve more documents (10 vs 3-5 in naive)
3. **Reranking**: Use cross-encoder (not LLM) to rerank by relevance
4. **Synthesis**: Combine reranked documents into structured answer
5. **Citation**: Include page numbers for each source

---

## Token Breakdown

**Pre-Retrieval Phase**:
- Query rewriting LLM call: 350 input + 60 output = 410 tokens

**Retrieval Phase**:
- Retrieved chunks (10 @ 500 tokens): 5,000 tokens

**Post-Retrieval Phase**:
- Cross-encoder reranking: 0 LLM tokens (separate model)

**Generation Phase**:
- Input: 400 (system) + 50 (query) + 2,500 (top-5) = 2,950 tokens
- Output: 300 tokens

**Total**: 410 + 5,000 + 2,950 + 300 = **8,660 tokens**

**Optimization**: Replace LLM reranking with cross-encoder → 59% reduction to 3,700 tokens

---

## When to Use

✅ **Best For**:
- Multi-concept queries (combines multiple rules)
- Accuracy-critical applications
- Rulebook synthesis (pros/cons comparison)

❌ **Not For**:
- Simple single-rule lookups (use Naive RAG)
- Latency-critical (<100ms)

---

## Code Example

```python
class AdvancedRag:
    async def retrieve_and_synthesis(self, query: str) -> str:
        # Step 1: Rewrite query
        rewrites = await self.rewrite_query(query)

        # Step 2: Retrieve with rewrites
        all_docs = []
        for rewrite in rewrites:
            docs = await self.vector_search(rewrite, top_k=5)
            all_docs.extend(docs)

        # Step 3: Deduplicate
        docs = self.deduplicate(all_docs)

        # Step 4: Cross-encoder reranking
        ranked = await self.cross_encoder.rank(query, docs)
        top_docs = ranked[:5]

        # Step 5: Generate
        prompt = f"""
        Synthesize these rules into a comprehensive answer.

        Query: {query}
        Documents: {format_docs(top_docs)}

        Provide structured answer with citations.
        """
        return await self.llm.generate(prompt)

    async def rewrite_query(self, query: str) -> list[str]:
        """Generate multiple query reformulations"""
        prompt = f"""
        Generate 3 different ways to ask this question:
        {query}

        Return as JSON list: ["question1", "question2", "question3"]
        """
        response = await self.llm.generate(prompt)
        return json.loads(response)
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Accuracy** | +10-12% improvement | Modest vs effort |
| **Latency** | 500ms-2s acceptable | Slower than naive |
| **Cost** | $0.013-0.032 (4-5x naive) | Expensive for simple queries |
| **Complexity** | Moderate implementation | Requires optimization |

---

## Integration

**Tier Level**: BALANCED tier in 5-tier architecture

**Use Cases**:
- Rule_lookup template: Multi-rule synthesis
- Resource_planning template: Weighted decision-making

---

## Research Sources

- [Advanced RAG Techniques](https://www.datacamp.com/blog/rag-advanced)
- [Evolution of RAGs: Naive, Advanced, Modular](https://www.marktechpost.com/2024/04/01/evolution-of-rags-naive-rag-advanced-rag-and-modular-rag-architectures/)

---

**Status**: Production-Ready | **MeepleAI Tier**: P0 Foundation
