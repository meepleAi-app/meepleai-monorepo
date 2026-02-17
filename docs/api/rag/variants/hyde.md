# HyDE (Hypothetical Document Embeddings)

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 3,950 |
| **Cost/Query** | $0.018 |
| **Accuracy** | +5% above naive |
| **Latency** | 500ms–2s |
| **Priority** | **P3** - Skip |

---

## Architecture Diagram

```
Query Input
    ↓
[Hypothetical Generation] Generate fake document
    ├─ Input: "Generate answer to question: {query}"
    ├─ Output: Hypothetical answer (200 tokens)
    ↓
[Embedding] Embed hypothetical answer
    ├─ No LLM tokens (embedding only)
    ↓
[Retrieval] Find similar documents
    ├─ Use hypothetical as embedding
    └─ 2,500 tokens (5 chunks)
    ↓
[Final Generation] Real answer from retrieved
    ├─ Input: 400 + 50 + 200 + 2,500 = 3,150 tokens
    └─ Output: 300 tokens
    ↓
Answer
```

---

## How It Works

1. **Generate Hypothetical Answer**:
   - LLM generates answer to query
   - May be completely wrong!
   - Cost: 300 input + 200 output = 500 tokens
2. **Embed Hypothetical**:
   - Convert to embedding vector
   - No LLM tokens (embedding service only)
3. **Retrieve Similar**:
   - Find real documents similar to hypothetical
   - Better for exploratory queries
4. **Generate Final Answer**:
   - Synthesize from retrieved docs

---

## Token Breakdown

**Hypothetical Generation**:
- Input: 250 + 50 = 300 tokens
- Output: 200 tokens (hypothetical answer)

**Embedding**: 0 LLM tokens (separate service)

**Retrieval**: 2,500 tokens (5 chunks)

**Final Generation**:
- Input: 400 + 50 + 200 (hyp) + 2,500 = 3,150 tokens
- Output: 300 tokens

**Total**: 3,950 tokens

---

## When to Use

✅ **For Exploratory Queries**:
- "What could go wrong?" → hypothetical helps find edge cases

❌ **NOT For**:
- Exact rule lookups (hypothetical likely wrong)
- Fact-based questions
- Where accuracy critical

---

## Code Example

```python
class HyDE:
    async def hyde_retrieve(
        self,
        query: str
    ) -> list[Document]:
        """Retrieve using hypothetical document"""

        # Step 1: Generate hypothetical answer
        hyp_prompt = f"""
        Generate a hypothetical answer to this question,
        even if you're not sure:
        {query}
        """
        hypothetical = await self.llm.generate(hyp_prompt)

        # Step 2: Embed hypothetical
        hyp_emb = await self.embedder.encode(hypothetical)

        # Step 3: Retrieve similar documents
        docs = await self.vector_db.search(hyp_emb, top_k=5)

        return docs

    async def execute_hyde(self, query: str) -> str:
        # Retrieve using hypothetical
        docs = await self.hyde_retrieve(query)

        # Generate final answer
        prompt = f"""
        Based on these documents, answer:
        {query}

        Documents:
        {self.format_docs(docs)}
        """

        return await self.llm.generate(prompt)
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Accuracy** | +5% (exploratory) | Hypothetical likely wrong |
| **Use Case** | Great for unknown domains | Risky for fact-based |
| **Cost** | $0.018 (reasonable) | Extra LLM call |

---

## Integration

**Tier Level**: Not recommended

**When Acceptable**:
- Exploratory searches only
- Unknown domains
- Cost of being wrong is low

---

## Research Sources

- [HyDE: Hypothetical Document Embeddings](https://arxiv.org/abs/2212.10496)

---

**Status**: Production-Ready | **MeepleAI Tier**: P3 Skip
