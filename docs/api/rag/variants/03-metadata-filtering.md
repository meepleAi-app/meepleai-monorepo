# Metadata Filtering RAG

**Stats**: 3,100 tokens/query | $0.012/query | +6% accuracy | 300-800ms latency | **Priority: P0**

## Architecture

```
User Query: "How does setup work in Wingspan?"
    ↓
┌────────────────────────────────────┐
│ Extract Filters (Self-Query LLM)  │
│ Output: {"game": "Wingspan",       │
│          "category": "setup"}      │
└────────────────────────────────────┘
    ↓
Vector DB: Apply metadata filters BEFORE vector search
    ↓
Filtered Search Space (1000 chunks → 200 chunks)
    ↓
Vector Search on Filtered Set (4 highly relevant chunks)
    ↓
Generate (2,450 tokens vs 2,950 unfiltered)
```

## How It Works

Metadata filtering reduces the search space before vector similarity, dramatically improving precision. Instead of searching across all rulebooks, the system first filters to relevant game and category (setup, combat, scoring) using structured metadata, then performs vector search only within that subset.

The Self-Query approach uses an LLM to extract filters from natural language queries. When a user asks "How does setup work in Wingspan?", the LLM outputs structured JSON filters: `{"game": "Wingspan", "category": "setup"}`. The vector database applies these filters before similarity search, eliminating irrelevant chunks from other games or rule sections.

This produces two benefits: (1) fewer irrelevant chunks in top-K results means better answer quality, and (2) fewer chunks needed overall means lower token costs. Instead of retrieving 5 mixed-relevance chunks, the system retrieves 4 highly-relevant filtered chunks, reducing context by 20% while improving accuracy by 6%.

For multi-game knowledge bases like MeepleAI, metadata filtering is critical to prevent cross-game rule contamination (e.g., Wingspan setup instructions mixed with Scythe setup).

## Token Breakdown

**Filter Extraction** (Self-Query):
- Input: 250 (prompt) + 50 (query) = 300 tokens
- Output: 50 tokens (filter JSON)

**Filtered Retrieval**:
- Search space: 1000 → 200 chunks (filtered by game+category)
- Retrieved: 4 highly relevant chunks = 2,000 tokens (vs 5 mixed chunks = 2,500)

**Generation**:
- Input: 400 + 50 + 2,000 = 2,450 tokens
- Output: 300 tokens

**Total**: 300 + 50 + 2,450 + 300 = **3,100 tokens**

## When to Use

- **Multi-domain knowledge bases** (multi-game catalogs, multi-product documentation)
- **Categorized content** where metadata provides strong filtering signal (setup, combat, scoring)
- **High precision requirements** where reducing irrelevant chunks is critical

## Code Example

```python
from anthropic import Anthropic
from qdrant_client import QdrantClient
from qdrant_client.models import Filter, FieldCondition, MatchValue

client = Anthropic(api_key="...")
qdrant = QdrantClient(host="localhost", port=6333)

def extract_filters(query: str) -> dict:
    """Use LLM to extract metadata filters from query."""

    response = client.messages.create(
        model="claude-3-5-haiku-20241022",
        max_tokens=100,
        messages=[{
            "role": "user",
            "content": f"""Extract metadata filters from this query as JSON.

Query: {query}

Output format: {{"game": "GameName", "category": "setup|combat|scoring|trading", "player_count": 2}}

Only include filters explicitly mentioned. If none, return {{}}.
"""
        }]
    )

    import json
    return json.loads(response.content[0].text)

def metadata_filtered_rag(query: str) -> str:
    """RAG with metadata filtering for multi-game knowledge base."""

    # Extract filters from query
    filters = extract_filters(query)

    # Build Qdrant filter
    conditions = []
    if "game" in filters:
        conditions.append(FieldCondition(
            key="game",
            match=MatchValue(value=filters["game"])
        ))
    if "category" in filters:
        conditions.append(FieldCondition(
            key="category",
            match=MatchValue(value=filters["category"])
        ))

    # Filtered vector search
    query_embedding = get_embedding(query)
    results = qdrant.search(
        collection_name="rulebooks",
        query_vector=query_embedding,
        query_filter=Filter(must=conditions) if conditions else None,
        limit=4
    )

    # Generate with filtered chunks
    context = "\n\n".join([hit.payload['text'] for hit in results])

    response = client.messages.create(
        model="claude-3-5-sonnet-20241022",
        max_tokens=300,
        messages=[{
            "role": "user",
            "content": f"Question: {query}\n\nContext:\n{context}\n\nAnswer:"
        }]
    )

    return response.content[0].text
```

## Integration

Metadata filtering integrates at **Layer 3 (Retrieval)** in TOMAC-RAG, enhancing vector search precision without changing other layers.

**Setup Requirements**:
1. Add metadata fields to chunk indexing (`game`, `category`, `player_count`, `expansion`)
2. Configure Qdrant collection with indexed metadata fields
3. Implement Self-Query LLM call for filter extraction

**Query Flow**:
1. **Layer 1**: Route query to appropriate strategy
2. **Layer 2**: Check semantic cache (optional)
3. **Layer 3**: Extract filters → Apply to vector search (THIS LAYER)
4. **Layer 4-6**: CRAG evaluation → generation → validation

**Compatibility**: Works seamlessly with hybrid search, contextual embeddings, and reranking. Filters reduce search space before any retrieval technique is applied.

## Sources

- [Metadata Filtering for RAG](https://unstructured.io/insights/how-to-use-metadata-in-rag-for-better-contextual-results)
- [Self-Query Retriever](https://python.langchain.com/docs/how_to/self_query/)
