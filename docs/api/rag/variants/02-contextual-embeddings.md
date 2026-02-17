# Contextual Embeddings RAG

**Stats**: 1,950 tokens/query | $0.007/query | +5% accuracy | Same latency | **Priority: P0**

## Architecture

```
Document Chunking
    ↓
┌─────────────────────────────────────────┐
│ Add Context Header to Each Chunk        │
│ "Game: Wingspan | Section: Setup | ..." │
└─────────────────────────────────────────┘
    ↓
Embed with Context (+80% embedding tokens, one-time)
    ↓
Query → Vector Search
    ↓
Better Precision: Retrieve 3 chunks instead of 5 (-40% retrieval tokens)
    ↓
Generate (1,950 tokens vs 2,950 traditional)
```

## How It Works

Anthropic's contextual embeddings innovation prepends document context to each chunk before embedding. Instead of embedding "Each player receives 5 food tokens" alone, the system embeds "Game: Wingspan | Section: Setup | Rule: Each player receives 5 food tokens." This context-enriched embedding dramatically improves search precision.

The key insight: embedding costs are one-time and amortized across millions of queries, while retrieval costs are per-query. By investing 80% more tokens during embedding (from 10 to 18 tokens per chunk), the system retrieves fewer chunks at query time while maintaining accuracy—3 chunks instead of 5 for a 40% reduction in retrieval tokens.

Traditional chunking produces context-free fragments that require more chunks to reconstruct meaning. Contextual embeddings make each chunk self-contained and semantically rich, improving both precision (fewer irrelevant chunks) and recall (better matching to user queries).

The net effect: 30% token reduction at query time (from 2,950 to 1,950 tokens) while improving accuracy by 5%. This is one of the highest ROI optimizations available.

## Token Breakdown

**Traditional Chunking**:
- Embedding: 10 tokens/chunk (one-time)
- Retrieval: 5 chunks × 500 tokens = 2,500 tokens
- Generation: 400 + 50 + 2,500 = 2,950 tokens

**Contextual Chunking**:
- Embedding: 18 tokens/chunk (+80%, one-time cost)
- Retrieval: 3 chunks × 500 tokens = 1,500 tokens (better precision)
- Generation: 400 + 50 + 1,500 = **1,950 tokens**

**Net Savings**: -34% query tokens, +5% accuracy

## When to Use

- **All RAG implementations** (universal best practice with no downside at query time)
- **Multi-game knowledge bases** where context (game name, section) is critical for disambiguation
- **Production systems** prioritizing long-term efficiency over initial embedding costs

## Code Example

```python
from typing import List, Dict
from anthropic import Anthropic

client = Anthropic(api_key="...")

def create_contextual_chunks(document: str, metadata: Dict[str, str]) -> List[Dict]:
    """Create contextual embeddings with prepended context headers."""

    chunks = split_document(document, chunk_size=500)
    contextual_chunks = []

    for chunk in chunks:
        # Prepend context header
        context_header = f"Game: {metadata['game']} | Section: {metadata['section']} | "
        contextual_text = context_header + chunk['text']

        # Embed with context
        embedding = get_embedding(contextual_text)

        contextual_chunks.append({
            "text": chunk['text'],  # Store original for generation
            "contextual_text": contextual_text,  # For search display
            "embedding": embedding,
            "metadata": metadata
        })

    return contextual_chunks

def contextual_rag(query: str, top_k: int = 3) -> str:
    """RAG with contextual embeddings - fewer chunks needed."""

    # Retrieve with better precision (top_k=3 instead of 5)
    query_embedding = get_embedding(query)
    chunks = vector_search(query_embedding, top_k=3)

    # Generate with fewer chunks
    context = "\n\n".join([c['text'] for c in chunks])

    response = client.messages.create(
        model="claude-3-5-sonnet-20241022",
        max_tokens=300,
        messages=[{
            "role": "user",
            "content": f"Question: {query}\n\nContext:\n{context}\n\nAnswer:"
        }]
    )

    return response.content[0].text

def get_embedding(text: str) -> List[float]:
    """Get embedding from your embedding service."""
    # Use sentence-transformers or OpenAI embeddings
    from sentence_transformers import SentenceTransformer
    model = SentenceTransformer('all-MiniLM-L6-v2')
    return model.encode(text).tolist()
```

## Integration

Contextual embeddings integrate at the **data preparation layer** (before indexing) and affect **Layer 3 (Retrieval)** efficiency in TOMAC-RAG.

**One-Time Setup**:
1. Re-process all documents with contextual chunking
2. Re-embed chunks with context headers (80% more embedding tokens)
3. Re-index in vector database (Qdrant)

**Query-Time Benefits** (automatic):
- Layer 3 retrieves 3 chunks instead of 5 (-40% tokens)
- Layer 5 generates with less context (1,950 vs 2,950 tokens)
- Layer 6 validation benefits from better chunk relevance

**Compatibility**: Works with all other techniques (hybrid search, reranking, CRAG). Simply enhances the quality of retrieved chunks.

## Sources

- [Magic Behind Anthropic's Contextual RAG](https://www.analyticsvidhya.com/blog/2024/11/anthropics-contextual-rag/)
- [Contextual Retrieval](https://www.anthropic.com/news/contextual-retrieval)
