# Advanced RAG

**Stats**: 3,700 tokens/query (optimized) | $0.013/query | +10% accuracy | 500ms-2s latency | **Priority: P0**

## Architecture

```
Pre-Retrieval Phase
    ↓
Query Rewriting (optional, 410 tokens)
    ↓
Retrieval Phase (10 chunks, 5,000 tokens)
    ↓
Post-Retrieval Phase
    ↓
┌─────────────────────────────────┐
│ Cross-Encoder Reranking         │  (0 LLM tokens)
│ Select Top 5 Reranked           │
└─────────────────────────────────┘
    ↓
Generation Phase (2,950 tokens)
    ↓
Total: 3,700 tokens (optimized) vs 9,000 tokens (full LLM reranking)
```

## How It Works

Advanced RAG extends Naive RAG with three enhancement phases: pre-retrieval (query optimization), retrieval (over-retrieval with 10 chunks), and post-retrieval (reranking to select top 5). This multi-stage pipeline improves accuracy by 10-12% over naive approaches.

Pre-retrieval can include query rewriting ("How do I win?" → "What are the victory conditions?"), expansion (add synonyms), or decomposition (split complex queries). For token efficiency, MeepleAI skips this phase for simple queries, using it only when complexity analysis indicates benefit.

The retrieval phase over-retrieves 10 candidates to increase recall. Post-retrieval reranking uses a cross-encoder model (not LLM) to score relevance and select the top 5 chunks for generation. This two-stage retrieval (cast wide net → filter precisely) significantly outperforms single-stage retrieval.

The optimized version achieves 3,700 tokens by using cross-encoder reranking instead of LLM-based reranking, saving ~5,300 tokens per query while maintaining accuracy gains.

## Token Breakdown

**Pre-Retrieval** (optional query rewriting):
- Input: 300 (prompt) + 50 (query) = 350 tokens
- Output: 60 tokens
- **Skip for simple queries to save 410 tokens**

**Retrieval**:
- Over-retrieve: 10 chunks × 500 = 5,000 tokens (not sent to LLM)

**Post-Retrieval** (cross-encoder reranking):
- Separate model inference: **0 LLM tokens**
- Select top 5: 2,500 tokens for generation

**Generation**:
- Input: 400 + 50 + 2,500 = 2,950 tokens
- Output: 300 tokens

**Optimized Total**: **3,700 tokens** (skipping pre-retrieval, using cross-encoder)
**Full Total**: 9,000 tokens (with query rewriting + LLM reranking)

## When to Use

- **BALANCED tier** (default for Regular/Editor users in TOMAC-RAG)
- **Production systems** requiring accuracy improvement over Naive RAG without excessive costs
- **Complex queries** where over-retrieval + reranking significantly improves answer quality

## Code Example

```python
from anthropic import Anthropic
from sentence_transformers import CrossEncoder

client = Anthropic(api_key="...")
reranker = CrossEncoder('cross-encoder/ms-marco-MiniLM-L6-v2')

def advanced_rag(query: str, use_query_rewriting: bool = False) -> str:
    """Advanced RAG with optional pre-retrieval and post-retrieval reranking."""

    # Pre-Retrieval: Query rewriting (optional)
    if use_query_rewriting and is_complex_query(query):
        rewritten = rewrite_query(query)
    else:
        rewritten = query

    # Retrieval: Over-retrieve candidates
    query_embedding = get_embedding(rewritten)
    candidates = vector_search(query_embedding, top_k=10)

    # Post-Retrieval: Rerank with cross-encoder
    pairs = [(rewritten, chunk['text']) for chunk in candidates]
    scores = reranker.predict(pairs)
    ranked_chunks = sorted(
        zip(candidates, scores),
        key=lambda x: x[1],
        reverse=True
    )[:5]  # Top 5

    # Generation
    context = "\n\n".join([c[0]['text'] for c in ranked_chunks])
    response = client.messages.create(
        model="claude-3-5-sonnet-20241022",
        max_tokens=300,
        messages=[{
            "role": "user",
            "content": f"Question: {query}\n\nContext:\n{context}\n\nAnswer:"
        }]
    )

    return response.content[0].text

def rewrite_query(query: str) -> str:
    """Rewrite query for better retrieval (optional)."""
    response = client.messages.create(
        model="claude-3-5-haiku-20241022",
        max_tokens=100,
        messages=[{
            "role": "user",
            "content": f"Rewrite this query to be more specific for rules lookup:\n\n{query}"
        }]
    )
    return response.content[0].text

def is_complex_query(query: str) -> bool:
    """Heuristic: queries >10 words or with AND/OR likely benefit from rewriting."""
    return len(query.split()) > 10 or any(kw in query.lower() for kw in ['and', 'or', 'but'])
```

## Integration

Advanced RAG serves as the **BALANCED tier** foundation in TOMAC-RAG, integrating across multiple layers.

**Layer Integration**:
1. **Layer 1 (Routing)**: Complexity analysis determines if query rewriting is beneficial
2. **Layer 2 (Cache)**: Check semantic cache before expensive advanced retrieval
3. **Layer 3 (Retrieval)**: Over-retrieve 10 chunks, apply cross-encoder reranking
4. **Layer 4 (CRAG)**: Evaluate reranked chunks for correctness
5. **Layer 5 (Generation)**: Generate with top 5 reranked chunks
6. **Layer 6 (Validation)**: Validate answer against reranked context

**Optimization Knobs**:
- **Query Rewriting**: Enable for complexity >5, disable for simple lookups
- **Reranking Model**: `ms-marco-MiniLM` (fast) vs `bge-reranker-large` (accurate)
- **Top-K**: Retrieve 10 → rerank to 5 (balance recall vs context size)

## Sources

- [Advanced RAG Techniques](https://www.datacamp.com/blog/rag-advanced)
- [Advancing LLMs: Advanced vs Modular RAG](https://zilliz.com/blog/advancing-llms-native-advanced-modular-rag-approaches)
