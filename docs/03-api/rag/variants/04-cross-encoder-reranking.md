# Cross-Encoder Reranking RAG

**Stats**: 3,250 tokens/query | $0.013/query | +8% accuracy | 500ms-1s latency | **Priority: P0**

## Architecture

```
Vector Search → Retrieve 10 chunks (over-retrieve)
    ↓
┌──────────────────────────────────────────┐
│ Cross-Encoder Reranking (Separate Model) │
│ - NOT LLM (0 LLM tokens)                 │
│ - Models: ms-marco-MiniLM, BGE-reranker  │
│ - Scores each (query, chunk) pair        │
└──────────────────────────────────────────┘
    ↓
Select Top 5 Reranked Chunks
    ↓
Generate (2,950 tokens vs 8,700 with LLM reranking)
```

## How It Works

Cross-encoder reranking uses a specialized neural model (not an LLM) to score the relevance of each retrieved chunk to the query. Unlike bi-encoder embeddings that encode query and chunks separately, cross-encoders process the query-chunk pair together, achieving significantly better relevance scoring.

The workflow: (1) vector search over-retrieves 10 candidates, (2) cross-encoder model scores each (query, chunk) pair without consuming LLM tokens, (3) top 5 reranked chunks are selected for generation. The cross-encoder runs on separate inference (CPU or small GPU), costing ~$0.001 per query—far cheaper than LLM-based reranking at ~$0.02 per query.

Popular cross-encoder models like `ms-marco-MiniLM-L6-v2` (90MB) or `bge-reranker-base` (280MB) can run on modest hardware. They provide 8-12% accuracy improvements over vector search alone by fixing semantic search failures (e.g., "not" handling, negations, precise requirements).

For BALANCED tier queries in TOMAC-RAG, cross-encoder reranking provides the best accuracy-to-cost ratio: +8% accuracy for only +$0.005 per query compared to naive RAG.

## Token Breakdown

**Retrieval**:
- Vector search: 10 chunks × 500 tokens = 5,000 tokens (not sent to LLM yet)

**Reranking** (Cross-Encoder Model):
- Inference: Separate model, **0 LLM tokens**
- Cost: ~$0.001 per query (model inference)

**Generation** (Top 5 after reranking):
- Input: 400 + 50 + 2,500 (top-5 chunks) = 2,950 tokens
- Output: 300 tokens

**Total LLM Tokens**: **3,250 tokens**
**Total Cost**: $0.012 (LLM) + $0.001 (cross-encoder) = **$0.013**

## When to Use

- **BALANCED tier** in TOMAC-RAG (default for Regular/Editor users)
- **Production systems** requiring accuracy boost without LLM reranking costs
- **High-precision retrieval** where top 5 chunks must be highly relevant

## Code Example

```python
from sentence_transformers import CrossEncoder
from anthropic import Anthropic
from typing import List, Dict

# Initialize cross-encoder model (load once at startup)
reranker = CrossEncoder('cross-encoder/ms-marco-MiniLM-L6-v2')
client = Anthropic(api_key="...")

def cross_encoder_rag(query: str) -> str:
    """RAG with cross-encoder reranking for improved precision."""

    # Step 1: Over-retrieve candidates
    query_embedding = get_embedding(query)
    candidates = vector_search(query_embedding, top_k=10)

    # Step 2: Rerank with cross-encoder
    pairs = [(query, chunk['text']) for chunk in candidates]
    scores = reranker.predict(pairs)

    # Sort by relevance score (descending)
    ranked_chunks = sorted(
        zip(candidates, scores),
        key=lambda x: x[1],
        reverse=True
    )

    # Step 3: Select top 5 reranked
    top_chunks = [chunk for chunk, score in ranked_chunks[:5]]
    context = "\n\n".join([c['text'] for c in top_chunks])

    # Step 4: Generate with reranked context
    response = client.messages.create(
        model="claude-3-5-sonnet-20241022",
        max_tokens=300,
        messages=[{
            "role": "user",
            "content": f"Question: {query}\n\nContext:\n{context}\n\nAnswer based on the provided context."
        }]
    )

    return response.content[0].text

def vector_search(embedding: List[float], top_k: int) -> List[Dict]:
    """Retrieve candidates from vector database."""
    from qdrant_client import QdrantClient
    qdrant = QdrantClient(host="localhost", port=6333)

    results = qdrant.search(
        collection_name="rulebooks",
        query_vector=embedding,
        limit=top_k
    )

    return [{"text": hit.payload['text'], "score": hit.score} for hit in results]
```

## Integration

Cross-encoder reranking integrates between **Layer 3 (Retrieval)** and **Layer 5 (Generation)** in TOMAC-RAG as an optional enhancement layer.

**Enhanced Flow**:
1. **Layer 3**: Vector search retrieves 10 candidates (over-retrieve)
2. **Layer 3.5** (NEW): Cross-encoder reranks to top 5 (THIS LAYER)
3. **Layer 4**: CRAG evaluates reranked chunks
4. **Layer 5**: Generate with top 5 reranked context
5. **Layer 6**: Validate answer quality

**Deployment**:
- Run cross-encoder as sidecar service (Python FastAPI)
- Endpoint: `POST /rerank` with `{query, chunks}` payload
- Model: Load `ms-marco-MiniLM-L6-v2` at startup (90MB RAM)
- Latency: 200-400ms for 10 chunks on CPU

**Alternative Models**:
- `bge-reranker-base` (280MB, +2% accuracy, +100ms latency)
- `bge-reranker-large` (1.3GB, +4% accuracy, +300ms latency)

## Sources

- [Production Retrievers: ColBERT, SPLADE](https://machine-mind-ml.medium.com/production-rag-that-works-hybrid-search-re-ranking-colbert-splade-e5-bge-624e9703fa2b)
- [Cross-Encoder Reranking](https://www.sbert.net/examples/applications/cross-encoder/README.html)
