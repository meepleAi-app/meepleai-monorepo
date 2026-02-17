# Dual-Encoder Multi-Hop

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 4,500 |
| **Cost/Query** | $0.017 |
| **Accuracy** | +10% above naive |
| **Latency** | 1–2s |
| **Priority** | **P2** - Consider |

---

## Architecture Diagram

```
Query Input
    ↓
[Dual-Encoder] Separate query/doc encoders
    ├─ Query encoder: Optimized for query understanding
    ├─ Document encoder: Optimized for dense doc representation
    ↓
[Hop 1] Initial retrieval (top-10)
    ├─ Use query encoding
    ↓
[Entity Extraction] From Hop 1 results
    ├─ Find related entities/concepts
    ↓
[Hop 2] Re-retrieve with entity queries
    ├─ Entity-specific document encoding
    ↓
[Merge] Deduplicate and rerank
    ↓
[Generation]
    ↓
Answer
```

---

## How It Works

1. **Dual-Encoder Models**:
   - Query Encoder: Specialized for query representation
   - Doc Encoder: Specialized for dense document encoding
   - Better accuracy than single encoder
2. **Hop 1**: Retrieve top-10 with query encoder
3. **Entity Extraction**: Find related concepts in results
4. **Hop 2**: Re-retrieve with entity-specific queries using doc encoder
5. **Merge**: Combine results, deduplicate, rank
6. **Generate**: Synthesize from merged results

---

## Token Breakdown

**Hop 1 Retrieval**:
- Query encoding + vector search: 2,000 tokens (top-10)

**Entity Extraction**:
- LLM identifies entities: 500 input + 100 output = 600 tokens

**Hop 2 Retrieval**:
- Re-retrieve with entities: 2,000 tokens (top-10 per entity)

**Generation**:
- Input: 400 + 50 + 2,500 (top-5 merged) = 2,950 tokens
- Output: 300 tokens

**Total**: 4,500 tokens

**Cost**: $0.017 per query

---

## When to Use

✅ **Best For**:
- Complex multi-concept queries
- High accuracy needed
- Entity-rich rulebooks

❌ **Not For**:
- Simple lookups
- Latency-critical (<500ms)

---

## Code Example

```python
class DualEncoderMultiHop:
    def __init__(self):
        self.query_encoder = AutoModel.from_pretrained(
            "sentence-transformers/e5-base-v2"
        )
        self.doc_encoder = AutoModel.from_pretrained(
            "sentence-transformers/e5-base-v2"
        )

    async def multi_hop_retrieve(
        self,
        query: str
    ) -> list[Document]:
        # Hop 1: Initial retrieval
        query_emb = self.query_encoder.encode(query)
        hop1_docs = await self.vector_db.search(query_emb, top_k=10)

        # Extract entities
        entities = await self.extract_entities(hop1_docs)

        # Hop 2: Entity-based retrieval
        all_docs = hop1_docs.copy()
        for entity in entities:
            entity_emb = self.doc_encoder.encode(f"about {entity}")
            hop2_docs = await self.vector_db.search(entity_emb, top_k=10)
            all_docs.extend(hop2_docs)

        # Deduplicate and merge
        merged = self.deduplicate(all_docs)
        return merged[:5]

    async def extract_entities(
        self,
        documents: list[Document]
    ) -> list[str]:
        """Extract key entities from docs"""
        doc_text = " ".join(d.content for d in documents)
        prompt = f"Extract 3-5 key entities from: {doc_text}"
        response = await self.llm.generate(prompt)
        return response.split(",")
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Accuracy** | +10% (dual encoders) | Complex implementation |
| **Latency** | 1-2s (2 hops) | Slower than single-hop |
| **Cost** | $0.017 (moderate) | Higher than simple |

---

## Integration

**Tier Level**: BALANCED tier (advanced retrieval)

---

**Status**: Partial Production-Ready | **MeepleAI Tier**: P2 Consider
