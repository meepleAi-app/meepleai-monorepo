# Query Decomposition RAG

**Stats**: 6,550 tokens/query | $0.026/query | +12% accuracy | 2-4s latency | **Priority: P1**

## Architecture

```
Complex Query: "How do combat AND trading work together?"
    ↓
┌────────────────────────────────────────┐
│ Decomposition LLM Call                 │
│ Input: 350 tokens                      │
│ Output: 3 sub-queries (150 tokens)     │
│   1. "How does combat work?"           │
│   2. "How does trading work?"          │
│   3. "How do they interact?"           │
└────────────────────────────────────────┘
    ↓
Parallel Retrieval (3 sub-queries → 7,500 tokens)
    ↓
Deduplication (7,500 → 5,000 unique tokens)
    ↓
Synthesis (5,650 tokens)
    ↓
Total: 6,550 tokens
```

## How It Works

Query decomposition breaks complex multi-concept queries into simpler sub-queries, retrieves context for each independently, then synthesizes a comprehensive answer. This addresses a key RAG limitation: single embedding vectors struggle to represent queries with multiple distinct concepts.

The workflow: (1) LLM decomposes complex query into 2-4 sub-queries, (2) each sub-query is embedded and retrieves relevant chunks in parallel, (3) deduplication removes overlapping chunks across retrievals, (4) synthesis LLM combines all findings into a coherent answer addressing the original complex query.

For example, "How do combat AND trading work together in Scythe?" decomposes to: (a) "Combat rules in Scythe", (b) "Trading rules in Scythe", (c) "Combat-trading interactions in Scythe". Each sub-query retrieves focused context, and synthesis combines insights that a single query vector would miss.

The token cost is moderate (3.3x naive RAG) because parallel retrieval may return overlapping chunks (same rules section mentions both concepts), and deduplication reduces final context from 7,500 to ~5,000 tokens.

## Token Breakdown

**Decomposition Phase**:
- Input: 300 (decomposition prompt) + 50 (complex query) = 350 tokens
- Output: 150 tokens (3 sub-queries @ 50 tokens each)

**Parallel Retrieval** (3 sub-queries):
- Sub-query 1: 2,500 tokens (5 chunks)
- Sub-query 2: 2,500 tokens (5 chunks)
- Sub-query 3: 2,500 tokens (5 chunks)
- **Total**: 7,500 tokens retrieved

**Deduplication**:
- Semantic overlap removal: 7,500 → 5,000 unique tokens

**Synthesis Phase**:
- Input: 500 (synthesis prompt) + 150 (sub-queries) + 5,000 (deduplicated docs) = 5,650 tokens
- Output: 400 tokens (comprehensive answer)

**Total Input**: 350 + 5,650 = 6,000 tokens
**Total Output**: 150 + 400 = 550 tokens
**Grand Total**: **6,550 tokens**

## When to Use

- **Complex multi-concept queries** with AND/OR logic (e.g., "combat AND trading", "setup OR teardown")
- **Comparison queries** requiring separate retrieval for each entity (e.g., "Wingspan vs Scythe")
- **PRECISE tier** where comprehensive multi-faceted answers justify higher token costs

## Code Example

```python
from anthropic import Anthropic
from typing import List

client = Anthropic(api_key="...")

def query_decomposition_rag(query: str) -> str:
    """RAG with query decomposition for complex multi-concept queries."""

    # Step 1: Decompose complex query into sub-queries
    decomposition = client.messages.create(
        model="claude-3-5-haiku-20241022",
        max_tokens=150,
        messages=[{
            "role": "user",
            "content": f"""Break this complex query into 2-4 simpler sub-queries.

Query: {query}

Output format (JSON array):
["sub-query 1", "sub-query 2", "sub-query 3"]
"""
        }]
    )

    import json
    sub_queries = json.loads(decomposition.content[0].text)

    # Step 2: Parallel retrieval for each sub-query
    all_chunks = []
    for sub_query in sub_queries:
        embedding = get_embedding(sub_query)
        chunks = vector_search(embedding, top_k=5)
        all_chunks.extend(chunks)

    # Step 3: Deduplicate overlapping chunks
    unique_chunks = deduplicate_by_text_similarity(all_chunks)
    context = "\n\n".join([c['text'] for c in unique_chunks])

    # Step 4: Synthesize comprehensive answer
    synthesis = client.messages.create(
        model="claude-3-5-sonnet-20241022",
        max_tokens=400,
        messages=[{
            "role": "user",
            "content": f"""Original question: {query}

Sub-questions explored:
{chr(10).join(f"- {sq}" for sq in sub_queries)}

Context from rulebook:
{context}

Provide a comprehensive answer that addresses all aspects of the original question.
"""
        }]
    )

    return synthesis.content[0].text

def deduplicate_by_text_similarity(chunks: List[dict], threshold: float = 0.85) -> List[dict]:
    """Remove chunks with >85% text similarity."""
    from difflib import SequenceMatcher
    unique = []

    for chunk in chunks:
        is_duplicate = False
        for existing in unique:
            similarity = SequenceMatcher(None, chunk['text'], existing['text']).ratio()
            if similarity > threshold:
                is_duplicate = True
                break
        if not is_duplicate:
            unique.append(chunk)

    return unique
```

## Integration

Query decomposition integrates as a **pre-retrieval enhancement** in TOMAC-RAG, activating before Layer 3 for complex queries.

**Flow for Complex Queries**:
1. **Layer 1 (Routing)**: Detect multi-concept query (AND/OR keywords, >15 words)
2. **Layer 2.5** (NEW): Decompose into sub-queries (THIS LAYER)
3. **Layer 3**: Parallel retrieval for each sub-query
4. **Layer 3.5**: Deduplicate overlapping chunks
5. **Layer 4**: CRAG evaluation on deduplicated set
6. **Layer 5**: Synthesis generation addressing original query
7. **Layer 6**: Validate comprehensive answer

**Activation Heuristics**:
- Keywords: "and", "or", "but", "versus", "vs", "compared to"
- Query length: >15 words
- Multiple noun phrases: >3 distinct concepts
- User tier: Editor/Admin only (expensive)

## Sources

- [Query Transformations: Decomposition](https://dev.to/jamesli/in-depth-understanding-of-rag-query-transformation-optimization-multi-query-problem-decomposition-and-step-back-27jg)
- [Query Decomposition & Reasoning](https://haystack.deepset.ai/blog/query-decomposition)
