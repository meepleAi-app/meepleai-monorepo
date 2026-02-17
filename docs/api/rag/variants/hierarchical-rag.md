# Hierarchical RAG

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 5,750 |
| **Cost/Query** | $0.021 |
| **Accuracy** | +9% above naive |
| **Latency** | 1–2s |
| **Priority** | **P2** - Context |

---

## Architecture Diagram

```
Knowledge Base Preparation (One-time)
    ↓
[Small Chunks] 100-200 tokens (precision)
[Parent Documents] 1,000+ tokens (context)
    ↓
[Retrieval] Find small chunks (1,000 tokens)
    ↓
[Expansion] Return parent documents
    ├─ 5 small chunks retrieved
    ├─ Expand to 5 parent docs @ 1,000 each
    └─ Total: 5,000 tokens (full context)
    ↓
[Generation] With full parent context
    ↓
Answer (full context, precise chunks)
```

---

## How It Works

1. **Small Chunks**: Index granular chunks (100-200 tokens)
   - Better search precision
   - Exact rule matching
2. **Parent Documents**: Store full-context parents
   - Provide surrounding context
   - Preserve rule relationships
3. **Retrieval**: Search small chunks (more relevant)
4. **Expansion**: Fetch parent documents for context
5. **Generation**: Synthesize with full context

---

## Token Breakdown

**Small Chunk Retrieval**:
- 1,000 tokens (5 small chunks @ 200 each)

**Parent Document Expansion**:
- 5,000 tokens (5 parent docs @ 1,000 each)

**Generation**:
- Input: 400 + 50 + 5,000 = 5,450 tokens
- Output: 300 tokens

**Total**: 5,750 tokens

**Cost**: $0.021 per query

---

## When to Use

✅ **Best For**:
- Rules spanning multiple sections
- Setup procedures (need full context)
- When precision + context both needed

❌ **Not For**:
- Single-sentence rules
- Simple lookups

---

## Code Example

```python
class HierarchicalRag:
    async def prepare_hierarchy(
        self,
        rulebook_text: str
    ):
        """Prepare small chunks + parent documents"""

        # Split into parent sections
        sections = self.split_by_section(rulebook_text)

        # For each section, create small chunks
        for section in sections:
            small_chunks = self.create_small_chunks(
                section.content,
                chunk_size=150,
                overlap=50
            )

            # Index small chunks with parent reference
            for chunk in small_chunks:
                await self.vector_db.add(
                    content=chunk.content,
                    embedding=await self.embedder.encode(chunk),
                    metadata={
                        "parent_id": section.id,
                        "page": section.page,
                        "section": section.title
                    }
                )

    async def retrieve_with_parents(
        self,
        query: str,
        top_k: int = 5
    ) -> list[Document]:
        """Retrieve small chunks, expand to parents"""

        # Search for small chunks
        small_chunks = await self.vector_db.search(query, top_k=top_k)

        # Retrieve parent documents
        parent_ids = set(
            c.metadata["parent_id"]
            for c in small_chunks
        )

        parents = await self.fetch_parents(list(parent_ids))

        return parents

    def create_small_chunks(
        self,
        text: str,
        chunk_size: int = 150,
        overlap: int = 50
    ) -> list[str]:
        """Create small overlapping chunks"""
        chunks = []
        start = 0

        while start < len(text):
            end = min(start + chunk_size, len(text))
            chunks.append(text[start:end])
            start = end - overlap

        return chunks
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Precision** | Small chunks improve search | +5,000 tokens for context |
| **Context** | Full parent docs preserved | More tokens overall |
| **Accuracy** | +9% improvement | Moderate latency (1-2s) |
| **Complexity** | Requires hierarchy structure | Additional indexing |

---

## Integration

**Tier Level**: BALANCED tier (context when needed)

**Optimization**: Sentence-Window variant (return ±3 sentences instead of full parent) saves 50%

---

## Research Sources

- [Parent Document Retrieval](https://github.com/NirDiamant/RAG_Techniques/blob/main/all_rag_techniques/hierarchical_indices.ipynb)
- [Document Hierarchy in RAG](https://medium.com/@nay1228/document-hierarchy-in-rag-boosting-ai-retrieval-efficiency-aa23f21b5fb9)

---

**Status**: Production-Ready | **MeepleAI Tier**: P2 Context
