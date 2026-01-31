# Naive RAG

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 2,000 |
| **Cost/Query** | $0.008 |
| **Accuracy** | Baseline |
| **Latency** | 50–200ms |
| **Priority** | **Baseline** - Reference |

---

## Architecture Diagram

```
Query Input
    ↓
[Retrieval] Vector search
    ├─ Find top-3 relevant chunks
    └─ 1,500 tokens
    ↓
[Generation] Synthesize
    ├─ Input: 300 (system) + 50 (query) + 1,500 (chunks) = 1,850 tokens
    └─ Output: 100-300 tokens
    ↓
Answer
```

---

## How It Works

1. **Retrieval**: Simple vector search
   - Embed query using encoder
   - Find top-3 most similar chunks
   - Cost: 1,500 tokens (3 chunks @ 500 tokens each)

2. **Generation**: Direct synthesis
   - Combine system prompt + query + chunks
   - Generate answer
   - Cost: 1,850 input + 200 output = 2,050 tokens

3. **Total**: ~2,000 tokens

---

## Token Breakdown

**System Prompt**: 200–300 tokens

**User Query**: 20–50 tokens

**Retrieved Chunks**: 1,500 tokens (3 @ 500 each)

**Generated Answer**: 100–300 tokens

**Total**: ~2,000 tokens

**Cost** (Claude 3.5 Sonnet @ $3/1M input, $15/1M output):
- Input: 1,800 × $3/1M = $0.0054
- Output: 200 × $15/1M = $0.003
- **Total**: ~$0.0084 per query

---

## When to Use

✅ **Best For**:
- Simple fact lookups
- One-concept queries
- Baseline/reference
- Testing

❌ **Not For**:
- Multi-concept questions
- High accuracy requirements
- Complex reasoning

---

## Code Example

```python
class NaiveRag:
    async def retrieve_and_generate(
        self,
        query: str
    ) -> str:
        """Simple RAG pipeline"""

        # Step 1: Retrieve top-3
        query_emb = await self.embedder.encode(query)
        docs = await self.vector_db.search(query_emb, top_k=3)

        # Step 2: Generate
        prompt = f"""
        You are a board game rules assistant.

        Retrieved Rules:
        {self.format_docs(docs)}

        Question: {query}

        Provide a direct answer with citations.
        """

        answer = await self.llm.generate(prompt)

        return answer

    def format_docs(self, docs: list[Document]) -> str:
        """Format docs with citations"""
        formatted = []
        for doc in docs:
            formatted.append(
                f"Rule {doc.id} (page {doc.metadata.get('page', '?')}): {doc.content}"
            )
        return "\n\n".join(formatted)
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Simplicity** | 3-component pipeline | Limited accuracy |
| **Cost** | ~$0.008/query (baseline) | Not for complex queries |
| **Latency** | 50-200ms (very fast) | No quality gates |
| **Implementation** | Easy to understand/implement | No validation |

---

## Integration

**Tier Level**: FAST tier (simple queries)

**Optimization Paths**:
- Add reranking → Advanced RAG
- Add validation → 5-tier
- Add caching → Memory-augmented
- Add web search → CRAG

---

## Use as Baseline

This is the reference implementation. All other variants are optimizations/enhancements to this baseline.

**Accuracy Improvements** (vs Naive):
- Advanced RAG: +10–12%
- CRAG: +12%
- Multi-Agent: +20%

**Token Multipliers** (vs Naive):
- Advanced RAG: 3.5–5x
- CRAG: 1.3x
- Multi-Agent: 6.5x

---

**Status**: Production-Ready | **MeepleAI Tier**: Baseline
