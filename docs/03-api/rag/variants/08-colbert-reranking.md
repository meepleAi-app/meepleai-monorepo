# ColBERT Reranking RAG

**Stats**: 3,250 tokens/query | $0.014/query | +12% accuracy | 500ms-1s latency | **Priority: P1**

## Architecture

```
Vector Search → Retrieve 10 chunks
    ↓
┌──────────────────────────────────────────┐
│ ColBERT Late Interaction Reranking       │
│ - Contextualized token embeddings        │
│ - MaxSim scoring per token pair          │
│ - NOT LLM (separate model inference)     │
│ - 0 LLM tokens consumed                  │
└──────────────────────────────────────────┘
    ↓
Select Top 5 Reranked Chunks
    ↓
Generate (2,950 tokens)
```

## How It Works

ColBERT (Contextualized Late Interaction over BERT) uses a novel "late interaction" mechanism for more accurate relevance scoring than cross-encoders. Instead of encoding the entire (query, document) pair as one vector, ColBERT encodes query and document tokens separately, then computes relevance through token-level maximum similarity (MaxSim).

The process: (1) encode query into contextualized token embeddings (each query word becomes a vector), (2) encode each candidate chunk into contextualized token embeddings, (3) for each query token, find the maximum similarity with any document token, (4) sum these max similarities as the relevance score.

This fine-grained matching captures nuanced relevance better than bi-encoder embeddings (which encode entire texts) or cross-encoders (which can't pre-compute document representations). ColBERT can pre-encode documents offline, making query-time reranking fast despite higher accuracy.

The trade-off: ColBERT inference costs ~$0.002 per query (2x cross-encoder) but provides +4% better accuracy, making it ideal for BALANCED tier where precision is critical but PRECISE tier costs are unjustified.

## Token Breakdown

**Retrieval**:
- Vector search: 10 chunks × 500 tokens = 5,000 tokens (not sent to LLM)

**ColBERT Reranking** (Separate Model):
- Late interaction scoring: **0 LLM tokens**
- Inference cost: ~$0.002 per query (model inference)

**Generation** (Top 5 after reranking):
- Input: 400 + 50 + 2,500 = 2,950 tokens
- Output: 300 tokens

**Total LLM Tokens**: **3,250 tokens**
**Total Cost**: $0.012 (LLM) + $0.002 (ColBERT) = **$0.014**

## When to Use

- **BALANCED tier (premium)** where +4% accuracy over cross-encoder justifies +$0.001 cost
- **Complex queries** with multi-faceted information needs (e.g., "combat AND trading")
- **High-stakes answers** requiring maximum retrieval precision without PRECISE tier costs

## Code Example

```python
from anthropic import Anthropic
import requests
from typing import List, Dict

client = Anthropic(api_key="...")

# ColBERT reranking service (deploy separately)
COLBERT_ENDPOINT = "http://localhost:8001/rerank"

def colbert_rag(query: str) -> str:
    """RAG with ColBERT late interaction reranking."""

    # Step 1: Over-retrieve candidates
    query_embedding = get_embedding(query)
    candidates = vector_search(query_embedding, top_k=10)

    # Step 2: Rerank with ColBERT
    colbert_response = requests.post(COLBERT_ENDPOINT, json={
        "query": query,
        "documents": [c['text'] for c in candidates]
    })
    scores = colbert_response.json()['scores']

    # Sort by ColBERT relevance scores
    ranked_chunks = sorted(
        zip(candidates, scores),
        key=lambda x: x[1],
        reverse=True
    )[:5]  # Top 5

    # Step 3: Generate with reranked context
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

# ColBERT Service (FastAPI example)
"""
from fastapi import FastAPI
from colbert import Searcher
from typing import List

app = FastAPI()
searcher = Searcher(checkpoint="colbert-ir/colbertv2.0")

@app.post("/rerank")
def rerank(query: str, documents: List[str]):
    scores = searcher.score(query, documents)
    return {"scores": scores.tolist()}
"""
```

## Integration

ColBERT reranking integrates between **Layer 3 (Retrieval)** and **Layer 5 (Generation)** as an enhanced alternative to cross-encoder reranking.

**Enhanced Flow**:
1. **Layer 3**: Vector search retrieves 10 candidates (over-retrieve)
2. **Layer 3.5**: ColBERT late interaction reranks to top 5 (THIS LAYER)
3. **Layer 4**: CRAG evaluates reranked chunks
4. **Layer 5**: Generate with top 5 reranked context
5. **Layer 6**: Validate answer quality

**Deployment**:
- Run ColBERT as separate Python service (FastAPI + ColBERT library)
- Model: `colbert-ir/colbertv2.0` (1.1GB checkpoint)
- Endpoint: `POST /rerank` with `{query, documents}` payload
- Hardware: GPU recommended for <500ms latency, CPU acceptable for <1s

**Performance Comparison**:
| Reranker | Accuracy | Cost/Query | Latency | Model Size |
|----------|----------|------------|---------|------------|
| Cross-Encoder | +8% | $0.013 | 400ms | 90MB |
| ColBERT | +12% | $0.014 | 600ms | 1.1GB |
| LLM Rerank | +14% | $0.033 | 1-2s | N/A |

## Sources

- [Production Retrievers: ColBERT, SPLADE](https://machine-mind-ml.medium.com/production-rag-that-works-hybrid-search-re-ranking-colbert-splade-e5-bge-624e9703fa2b)
- [ColBERT: Efficient and Effective Passage Search](https://arxiv.org/abs/2004.12832)
