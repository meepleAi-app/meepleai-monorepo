# Sentence Window Retrieval RAG

**Stats**: 3,250 tokens/query | $0.012/query | +7% accuracy | 500ms-1s latency | **Priority: P1**

## Architecture

```
Indexing: Embed individual sentences (granular)
    ↓
Query → Retrieve 5 relevant sentences (500 tokens)
    ↓
┌────────────────────────────────────┐
│ Window Expansion                   │
│ For each sentence: ±2 sentences    │
│ 500 + (5×2×2×100) = 2,500 tokens   │
└────────────────────────────────────┘
    ↓
Generate with expanded context (2,950 tokens)
```

## How It Works

Sentence Window retrieval combines granular search precision with contextual expansion. During indexing, documents are split into individual sentences and embedded separately. At query time, the most relevant sentences are retrieved, then expanded with ±N surrounding sentences to provide context.

This two-stage approach solves a key RAG challenge: search needs precision (find exact relevant sentence), but generation needs context (understand surrounding information). Traditional chunking (500 tokens) compromises both—too large for precise matching, too small for full context.

By indexing sentences (~100 tokens each), the system achieves precise matching to the most relevant information. The window expansion (±2 sentences before/after each match) reconstructs necessary context without including entire paragraphs. For 5 retrieved sentences with ±2 window, the total context is 5 + (5×2×2) = 25 sentences = 2,500 tokens.

For board game rules, this is ideal: retrieve the specific rule sentence ("Each player draws 5 cards"), then expand with setup context before and after to understand when/how this applies.

## Token Breakdown

**Sentence Retrieval**:
- Retrieve 5 relevant sentences: 5 × 100 = 500 tokens

**Window Expansion** (±2 sentences):
- Original 5 sentences: 500 tokens
- Before context: 5 sentences × 2 before × 100 = 1,000 tokens
- After context: 5 sentences × 2 after × 100 = 1,000 tokens
- **Total context**: 2,500 tokens

**Generation**:
- Input: 400 + 50 + 2,500 = 2,950 tokens
- Output: 300 tokens

**Total**: **3,250 tokens**

## When to Use

- **Precise rule extraction** where exact sentences matter (game rules, legal terms)
- **Context-dependent answers** requiring surrounding information for interpretation
- **Balance between precision and context** (better than both naive chunking and full paragraphs)

## Code Example

```python
from anthropic import Anthropic
from typing import List, Dict

client = Anthropic(api_key="...")

def sentence_window_rag(query: str, window_size: int = 2) -> str:
    """RAG with sentence-level retrieval and window expansion."""

    # Retrieve relevant sentences
    query_embedding = get_embedding(query)
    sentence_results = vector_search(
        query_embedding,
        top_k=5,
        collection="sentences"  # Indexed at sentence granularity
    )

    # Expand each sentence with surrounding context
    expanded_chunks = []
    for sentence in sentence_results:
        expanded = expand_sentence_window(
            sentence,
            window_before=window_size,
            window_after=window_size
        )
        expanded_chunks.append(expanded)

    # Deduplicate overlapping windows
    context = deduplicate_and_merge(expanded_chunks)

    # Generate with expanded context
    response = client.messages.create(
        model="claude-3-5-sonnet-20241022",
        max_tokens=300,
        messages=[{
            "role": "user",
            "content": f"Question: {query}\n\nContext:\n{context}\n\nAnswer based on the relevant rules."
        }]
    )

    return response.content[0].text

def expand_sentence_window(
    sentence: Dict,
    window_before: int,
    window_after: int
) -> str:
    """Expand sentence with surrounding context."""
    doc_id = sentence['doc_id']
    sentence_idx = sentence['sentence_index']

    # Fetch surrounding sentences from document
    before_sentences = get_sentences(
        doc_id,
        start=max(0, sentence_idx - window_before),
        end=sentence_idx
    )
    after_sentences = get_sentences(
        doc_id,
        start=sentence_idx + 1,
        end=sentence_idx + window_after + 1
    )

    # Combine: before + target + after
    return " ".join(
        before_sentences +
        [sentence['text']] +
        after_sentences
    )

def deduplicate_and_merge(chunks: List[str]) -> str:
    """Merge overlapping sentence windows, removing duplicates."""
    # Sort by document position, merge overlapping ranges
    # Implementation depends on sentence metadata tracking
    return "\n\n".join(set(chunks))  # Simplified
```

## Integration

Sentence Window integrates at **Layer 3 (Retrieval)** in TOMAC-RAG, requiring changes to indexing strategy and retrieval logic.

**Indexing Changes**:
1. Split documents into sentences (not 500-token chunks)
2. Store sentence metadata: `doc_id`, `sentence_index`, `section`
3. Embed each sentence individually
4. Index in Qdrant with sentence-level granularity

**Query-Time Flow**:
1. **Layer 3A**: Retrieve top 5 relevant sentences (granular search)
2. **Layer 3B**: Expand each with ±2 sentence window (THIS LAYER)
3. **Layer 3C**: Deduplicate overlapping windows
4. Proceed to Layer 4 (CRAG) with expanded context

**Window Size Tuning**:
- **±1 sentence**: ~1,500 tokens (minimal context, highest precision)
- **±2 sentences**: ~2,500 tokens (balanced, recommended)
- **±3 sentences**: ~3,500 tokens (full context, approaching parent document)

## Sources

- [Advanced RAG Techniques](https://www.falkordb.com/blog/advanced-rag/)
- [Hierarchical Indices](https://github.com/NirDiamant/RAG_Techniques/blob/main/all_rag_techniques/hierarchical_indices.ipynb)
