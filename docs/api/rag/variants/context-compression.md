# Context Compression

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 1,950 |
| **Cost/Query** | $0.008 |
| **Accuracy** | -3-5% (slight loss) |
| **Latency** | 200–500ms |
| **Priority** | **P2** - Optimization |

---

## Architecture Diagram

```
Query Input
    ↓
[Retrieval] Fetch documents (10)
    ├─ Input: 5,000 tokens
    ↓
[Compression] LongLLMLingua or similar
    ├─ Remove redundancy
    ├─ Keep key information
    └─ Output: 1,200 tokens (76% reduction)
    ↓
[Generation] Synthesize from compressed
    ├─ Input: 400 + 50 + 1,200 = 1,650 tokens
    └─ Output: 300 tokens
    ↓
Answer
```

---

## How It Works

1. **Retrieve**: Standard top-10 chunks (5,000 tokens)
2. **Compress**: LongLLMLingua algorithm
   - Identify redundant tokens
   - Preserve semantic information
   - 75-80% compression rate typical
3. **Generate**: Use compressed context (1,200 tokens)
4. **Trade-off**: Marginal accuracy loss for major token savings

---

## Token Breakdown

**Retrieval**:
- 10 chunks: 5,000 tokens

**Compression**:
- Separate model (not LLM): 5,000 → 1,200 tokens
- Cost: ~$0.001 (compression service)

**Generation**:
- Input: 1,650 tokens
- Output: 300 tokens

**Total**: 1,950 tokens

**Cost**: LLM $0.007 + Compression $0.001 = **$0.008 per query**

---

## When to Use

✅ **Best For**:
- Token-constrained environments
- Budget-conscious deployments
- FAQ where context can be compressed
- Fallback for budget overages

❌ **Not For**:
- Nuance-critical queries
- Complex rule synthesis

---

## Code Example

```python
class ContextCompression:
    async def compress_and_generate(
        self,
        query: str,
        documents: list[Document]
    ) -> str:
        """Retrieve, compress, and generate"""

        # Step 1: Retrieve
        docs_text = self.format_docs(documents)

        # Step 2: Compress using LongLLMLingua
        compressed = await self.compressor.compress(
            documents=docs_text,
            query=query,
            compression_ratio=0.25  # Keep 25% of tokens
        )

        # Step 3: Generate with compressed context
        prompt = f"""
        Using this compressed context, answer the question.

        Context: {compressed}

        Question: {query}

        Provide a direct answer with citations.
        """

        return await self.llm.generate(prompt)

    async def compress(
        self,
        documents: str,
        query: str,
        compression_ratio: float = 0.25
    ) -> str:
        """Compress documents using LongLLMLingua algorithm"""

        # Tokenize
        tokens = self.tokenizer.tokenize(documents)
        target_length = int(len(tokens) * compression_ratio)

        # Identify important tokens based on:
        # 1. TF-IDF similarity to query
        # 2. Position in text
        # 3. Semantic importance
        scores = self._calculate_token_importance(tokens, query)

        # Keep highest-scoring tokens
        important_indices = sorted(
            range(len(tokens)),
            key=lambda i: scores[i],
            reverse=True
        )[:target_length]

        # Reconstruct (preserve order)
        important_indices.sort()
        compressed = " ".join(tokens[i] for i in important_indices)

        return compressed
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Cost** | -75% tokens (massive savings) | -3-5% accuracy loss |
| **Latency** | 200-500ms (compression overhead) | Trade-off required |
| **Implementation** | Relatively simple | Requires compression service |
| **Use Case** | Great for token budgets | Not for critical queries |

---

## Integration

**Tier Level**: Optimization layer (can add to any tier)

**When to Use**:
- Last resort for budget overages
- FAST tier fallback
- Token-constrained deployments

**Compression Services**:
- LongLLMLingua (open-source)
- Cohere Rerank (API)
- Custom implementation

---

## Research Sources

- [RAG Cost Optimization: Cut Spending by 90%](https://app.ailog.fr/en/blog/guides/rag-cost-optimization)
- [LongLLMLingua: Context-Aware Compression](https://arxiv.org/abs/2310.06839)

---

**Status**: Production-Ready | **MeepleAI Tier**: P2 Optimization
