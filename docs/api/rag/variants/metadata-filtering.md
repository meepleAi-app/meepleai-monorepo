# Metadata Filtering RAG

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 3,100 |
| **Cost/Query** | $0.012 |
| **Accuracy** | +6% above naive |
| **Latency** | 300–800ms |
| **Priority** | **P0** - Multi-Game |

---

## Architecture Diagram

```
Query Input
    ↓
[Metadata Extraction] (Self-Query)
    ├─ Extract game name: "Wingspan"
    ├─ Extract category: "setup"
    └─ Generate filter: {"game": "Wingspan", "category": "setup"}
    ↓
[Filtered Retrieval] Reduce search space
    ├─ Vector search only in Wingspan rules
    ├─ Filter by category = "setup"
    └─ Result: 4 highly relevant chunks (not 5 mixed-relevance)
    ↓
[Generation]
    ├─ Input: 400 + 50 + 2,000 (4 chunks vs 5) = 2,450 tokens
    └─ Output: 300 tokens
    ↓
Answer (filtered for relevance)
```

---

## How It Works

1. **Self-Query** (if LLM-based):
   - LLM extracts metadata filters
   - Cost: 300 input + 50 output = 350 tokens
2. **Metadata-Indexed Chunks**:
   - Store metadata: game, category, page, complexity
   - Chunks tagged at index time
3. **Filtered Retrieval**:
   - Vector search only on filtered subset
   - Fewer irrelevant results
   - Smaller search space = better precision
4. **Generation**:
   - Synthesize from higher-quality chunks

---

## Token Breakdown

**Self-Query** (if using LLM):
- Input: 250 (prompt) + 50 (query) = 300 tokens
- Output: 50 tokens (filter JSON)

**Filtered Retrieval**:
- Search space reduced by 50-70%
- 2,000 tokens (4 chunks vs 5 mixed-relevance)

**Generation**:
- Input: 400 + 50 + 2,000 = 2,450 tokens
- Output: 300 tokens

**Total**: 3,100 tokens

**Cost**: $0.012 per query

---

## When to Use

✅ **Best For**:
- Multi-game knowledge base (filter by game)
- Categorized rules (filter by category)
- Large knowledge bases (filtering helps)

✅ **Advantages**:
- +6% accuracy improvement
- Lower cost than no filtering
- Reduces irrelevant results

---

## Code Example

```python
class MetadataFilteringRag:
    async def self_query(
        self,
        query: str
    ) -> dict:
        """Extract metadata filters from query"""

        prompt = f"""
        Extract metadata from this question:
        {query}

        Return JSON: {{"game": "...", "category": "...", "complexity": "..."}}
        """

        response = await self.llm.generate(prompt)
        return json.loads(response)

    async def retrieve_with_filters(
        self,
        query: str,
        top_k: int = 4
    ) -> list[Document]:
        """Retrieve with metadata filtering"""

        # Extract filters
        filters = await self.self_query(query)

        # Build Qdrant filter
        qdrant_filter = self._build_qdrant_filter(filters)

        # Vector search with filter
        query_emb = await self.embedder.encode(query)

        results = await self.qdrant.search(
            query_emb,
            query_filter=qdrant_filter,
            limit=top_k
        )

        return results

    def _build_qdrant_filter(self, filters: dict) -> dict:
        """Build Qdrant filter from metadata"""

        conditions = []

        if filters.get("game"):
            conditions.append({
                "key": "game",
                "match": {"value": filters["game"]}
            })

        if filters.get("category"):
            conditions.append({
                "key": "category",
                "match": {"value": filters["category"]}
            })

        if filters.get("complexity"):
            conditions.append({
                "key": "complexity",
                "match": {"value": filters["complexity"]}
            })

        return {"must": conditions} if conditions else None
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Accuracy** | +6% improvement | Self-query adds tokens |
| **Cost** | $0.012 (low) | Metadata extraction overhead |
| **Precision** | Fewer irrelevant results | Requires metadata at index time |
| **Latency** | 300-800ms (fast) | Filter extraction needed |

---

## Integration

**Tier Level**: FAST/BALANCED tier (quick optimization)

**Metadata to Tag**:
- game (required)
- category (section name)
- page (document location)
- complexity (rules difficulty)

**Filter Options**:
1. **LLM Self-Query**: Flexible but +350 tokens
2. **Semantic Router**: Faster, rule-based (~50 tokens)
3. **User-Provided**: If UI allows ("Filter by game: Wingspan")

---

## Research Sources

- [Metadata Filtering for RAG](https://unstructured.io/insights/how-to-use-metadata-in-rag-for-better-contextual-results)
- [Self-Querying Retrievers](https://python.langchain.com/docs/retrievers/self_query/)

---

**Status**: Production-Ready | **MeepleAI Tier**: P0 Multi-Game
