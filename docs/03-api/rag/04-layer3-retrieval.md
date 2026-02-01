# Layer 3: Modular Retrieval

**Purpose**: Adaptive retrieval based on strategy (FAST/BALANCED/PRECISE)

**Token Cost**: 1,500-8,000 tokens depending on strategy
**Latency**: 50ms-1s

---

## Retrieval Strategies

### FAST: Vector-Only Retrieval

**Flow**: Linear (single-hop)

```python
async def retrieve_fast(query: str, game_id: str = None) -> list[Document]:
    # Embedding
    embedding = await embed_minilm(query)  # MiniLM-L6-v2, 14.7ms

    # Vector search (Qdrant)
    results = await qdrant.search(
        collection_name="board_game_rules",
        query_vector=embedding,
        limit=3,  # Top-3 only
        query_filter={"game_id": game_id} if game_id else None
    )

    return [Document(content=r.payload["text"], metadata=r.payload) for r in results]
```

**Tokens**: ~1,500 (3 chunks @ 500 tokens each)
**Models**: MiniLM-L6-v2 (384 dimensions)

---

### BALANCED: Hybrid Search + Metadata

**Flow**: Conditional (with CRAG evaluation)

```python
async def retrieve_balanced(query: str, metadata_filters: dict = None) -> list[Document]:
    # Embedding (better model than FAST)
    embedding = await embed_e5_base(query)  # E5-Base-v2, 79ms

    # Parallel: Vector + BM25 keyword
    vector_results = await qdrant.search(
        collection_name="board_game_rules",
        query_vector=embedding,
        limit=10,
        query_filter=metadata_filters
    )

    keyword_results = await qdrant.scroll(
        collection_name="board_game_rules",
        scroll_filter=models.Filter(
            must=[
                models.FieldCondition(key="text", match=models.MatchText(text=query))
            ]
        ),
        limit=10
    )

    # Reciprocal Rank Fusion
    fused = reciprocal_rank_fusion(vector_results, keyword_results)

    return fused[:10]  # Top-10 after fusion
```

**Tokens**: ~3,500 (10 chunks, some duplicates removed)
**Models**: E5-Base-v2 or BGE-Base-v1.5 (768 dimensions)

---

### PRECISE: Multi-Hop Adaptive

**Flow**: Looping (iterative refinement)

```python
async def retrieve_precise(query: str, max_hops: int = 3) -> list[Document]:
    all_docs = []
    seen_ids = set()

    # Hop 1: Initial broad retrieval
    hop1 = await qdrant.search(query_vector=await embed_bge(query), limit=20)
    all_docs.extend(hop1)
    seen_ids.update(doc.id for doc in hop1)

    # Hop 2: Entity expansion
    entities = extract_entities(hop1)  # Extract game mechanics, terms
    hop2 = []
    for entity in entities[:5]:  # Top-5 entities
        entity_docs = await qdrant.search(
            query_vector=await embed_bge(entity),
            limit=5,
            query_filter={"must_not": [{"key": "id", "match": list(seen_ids)}]}
        )
        hop2.extend(entity_docs)

    all_docs.extend(hop2)
    seen_ids.update(doc.id for doc in hop2)

    # Hop 3: Cross-reference validation
    hop3 = await retrieve_related_rules(all_docs, exclude_ids=seen_ids)
    all_docs.extend(hop3)

    # Deduplicate
    unique_docs = deduplicate_by_id(all_docs)

    return unique_docs[:20]  # Top-20 total
```

**Tokens**: ~8,000 (20 chunks after deduplication)
**Models**: BGE-Base-v1.5

---

## Metadata Filtering

**Self-Query Extraction**:

```python
async def extract_metadata_filters(query: str) -> dict:
    """Use LLM to extract structured filters from natural language"""
    prompt = f'''
    Extract metadata filters from this query:
    "{query}"

    Available metadata: game_id, category, phase, complexity, page_range

    Return JSON: {{"game_id": str, "category": str, ...}} or {{}}
    '''

    response = await llm_haiku.generate(prompt, max_tokens=100)
    return json.loads(response.content)
```

**Token Cost**: +300 tokens for extraction (worth it for 30% retrieval improvement)

---

**See Also**: [Token Optimization](08-token-optimization.md) for retrieval efficiency techniques
