# Hypothetical Questions RAG

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 2,750 |
| **Cost/Query** | $0.010 |
| **Accuracy** | +5% above naive |
| **Latency** | <500ms (query time) |
| **Priority** | **P2** - FAQ Generation |

---

## Architecture Diagram

```
Knowledge Base Preparation (One-time)
    ↓
[Question Generation] For each document
    ├─ Input: 300 (prompt) + 1,000 (document) = 1,300 tokens
    ├─ Output: 150 tokens (3 questions per doc)
    └─ Amortized: ~0.015 tokens per query
    ↓
[Query Time]
    ├─ Match user query to generated questions
    ├─ Retrieve document with highest question match
    └─ 2,000 tokens (5 documents @ 400 each)
    ↓
[Generation] From matched documents
    ├─ Input: 400 + 50 + 2,000 = 2,450 tokens
    └─ Output: 300 tokens
    ↓
Answer
```

---

## How It Works

1. **One-Time Question Generation**:
   - For each document, generate 3-5 questions it could answer
   - Store questions with document reference
   - Cost: One-time 1,300 tokens per document

2. **Query-Time Matching**:
   - Compare user query against generated questions
   - Find documents with matching questions
   - Cost: Minimal (embedding similarity, not LLM)

3. **Retrieval & Generation**:
   - Retrieve matched documents
   - Synthesize answer
   - Cost: 2,750 tokens

---

## Token Breakdown

**Question Generation** (one-time per document, amortized):
- 1,300 tokens per doc / 100K queries = 0.013 tokens per query

**Query-Time Retrieval**:
- 2,000 tokens (matched documents)

**Generation**:
- Input: 2,450 tokens
- Output: 300 tokens

**Total**: 2,750 tokens

**Cost**: $0.010 per query

---

## When to Use

✅ **Best For**:
- FAQ generation (anticipate user questions)
- Rulebook Q&A (pre-written questions)
- Better recall for diverse phrasings

❌ **Not For**:
- Unexpected or novel queries
- Documents without clear questions

---

## Code Example

```python
class HypotheticalQuestionsRag:
    async def prepare_questions(
        self,
        rulebook_text: str
    ):
        """Generate hypothetical questions for documents"""

        # Split into documents
        documents = self.split_documents(rulebook_text)

        for doc in documents:
            # Generate questions this doc could answer
            prompt = f"""
            What 3-5 questions could this document answer?
            Document: {doc.content}

            Return JSON: ["question1", "question2", ...]
            """

            questions = await self.llm.generate(prompt)
            questions = json.loads(questions)

            # Store with document
            for question in questions:
                question_emb = await self.embedder.encode(question)

                await self.vector_db.add(
                    content=question,
                    embedding=question_emb,
                    metadata={
                        "type": "question",
                        "source_doc_id": doc.id,
                        "source_content": doc.content
                    }
                )

    async def retrieve_by_hypothetical_questions(
        self,
        query: str
    ) -> list[Document]:
        """Retrieve documents by matching to hypothetical questions"""

        # Encode user query
        query_emb = await self.embedder.encode(query)

        # Search for matching hypothetical questions
        question_matches = await self.vector_db.search(
            query_emb,
            top_k=5,
            filter={"type": "question"}
        )

        # Get source documents
        unique_docs = {}
        for match in question_matches:
            doc_id = match.metadata["source_doc_id"]
            if doc_id not in unique_docs:
                unique_docs[doc_id] = Document(
                    id=doc_id,
                    content=match.metadata["source_content"]
                )

        return list(unique_docs.values())

    async def execute_hypothetical_rag(
        self,
        query: str
    ) -> str:
        """Full pipeline using hypothetical questions"""

        # Retrieve by question matching
        docs = await self.retrieve_by_hypothetical_questions(query)

        # Generate answer
        prompt = f"""
        Based on these documents, answer:
        {query}

        Documents:
        {self.format_docs(docs)}
        """

        return await self.llm.generate(prompt)
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Recall** | Better for diverse phrasings | One-time question generation |
| **Cost** | Low query-time cost | Initial setup required |
| **Use Case** | Great for FAQ | Requires pre-thinking questions |
| **Efficiency** | Fast matching (no LLM needed) | Limited to anticipated questions |

---

## Integration

**Tier Level**: FAST tier (pre-generated questions)

**Best For**: FAQ knowledge bases with anticipated questions

---

## Research Sources

- Variant of HyDE concept applied in reverse

---

**Status**: Production-Ready | **MeepleAI Tier**: P2 FAQ Generation
