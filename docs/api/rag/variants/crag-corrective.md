# CRAG (Corrective RAG)

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 2,625 |
| **Cost/Query** | $0.010 |
| **Accuracy** | +12% above naive |
| **Latency** | 1–3s |
| **Priority** | **P1** - Rule Lookup |

---

## Architecture Diagram

```
Query Input
    ↓
[Retrieval] Fetch documents (10)
    ↓
[Evaluator] T5-large (NOT LLM)
    ├─ Correct? (50% of queries)
    ├─ Ambiguous? (30% of queries)
    └─ Incorrect? (20% of queries)
    ↓
[Conditional Path]
    ├─ Correct → Decompose-recompose → 1,450 tokens
    ├─ Ambiguous → Web search + internal → 3,450 tokens
    └─ Incorrect → Web search only → 2,950 tokens
    ↓
[Generation] Synthesize
    ↓
Answer
```

---

## How It Works

1. **Retrieval**: Standard top-10 chunks
2. **Evaluation** (T5-large, separate model):
   - Score 0-1: Are retrieved docs sufficient?
   - Classify: Correct / Ambiguous / Incorrect
3. **Conditional Generation**:
   - **Correct** (50%): Use retrieved docs as-is
   - **Ambiguous** (30%): Augment with web search
   - **Incorrect** (20%): Replace with web search
4. **Weighted Average**: 0.5×1700 + 0.3×3750 + 0.2×3250 = **2,625 tokens**

---

## Token Breakdown

**Path A: Correct (50% of queries)**
- Evaluation: 0 LLM tokens (T5 separate)
- Decompose-recompose: Extract key sentences (400 input, 200 output)
- Generation: 1,050 input + 250 output
- Total: 1,700 tokens

**Path B: Ambiguous (30%)**
- Web search: 5 results = 2,000 tokens
- Internal docs: 1,000 tokens
- Generation: 3,450 input + 300 output
- Total: 3,750 tokens

**Path C: Incorrect (20%)**
- Web search: 5 results = 2,500 tokens
- Generation: 2,950 input + 300 output
- Total: 3,250 tokens

**Weighted Average**: 0.5×1,700 + 0.3×3,750 + 0.2×3,250 = **2,625 tokens**

**Cost** (T5 evaluation + LLM):
- Evaluator: $0.002 per query
- LLM: $0.008 per query
- **Total**: $0.010 per query

---

## When to Use

✅ **Best For**:
- Rule lookups (high correctness path)
- Multi-game knowledge base (evaluation prevents hallucinations)
- Critical accuracy requirements
- Fact-checking scenarios

❌ **Not For**:
- Web search unavailable
- Offline-only scenarios

---

## Code Example

```python
class CorrectedRag:
    async def execute_crag(self, query: str) -> CragResponse:
        # Step 1: Retrieve
        docs = await self.retrieve(query, top_k=10)

        # Step 2: Evaluate (T5, not LLM)
        evaluation = await self.evaluate_documents(query, docs)

        # Step 3: Route based on evaluation
        if evaluation.score > 0.8:
            # Path A: Correct
            answer = await self.path_correct(query, docs)
        elif evaluation.score > 0.5:
            # Path B: Ambiguous
            answer = await self.path_ambiguous(query, docs)
        else:
            # Path C: Incorrect
            answer = await self.path_incorrect(query)

        return CragResponse(
            answer=answer,
            path=evaluation.path,
            confidence=evaluation.score
        )

    async def evaluate_documents(
        self,
        query: str,
        documents: list[Document]
    ) -> EvaluationResult:
        """Use T5 evaluator (NOT LLM)"""

        # T5 model for document evaluation
        docs_text = " ".join(doc.content for doc in documents)

        # Score: Does document support query?
        score = await self.t5_evaluator.score(query, docs_text)

        # Classification
        if score > 0.8:
            path = "correct"
        elif score > 0.5:
            path = "ambiguous"
        else:
            path = "incorrect"

        return EvaluationResult(score=score, path=path)

    async def path_correct(self, query: str, docs: list[Document]) -> str:
        """Use internal docs with decompose-recompose"""

        # Extract key sentences
        key_sentences = []
        for doc in docs[:3]:
            sentences = doc.content.split(".")
            # Score sentences by relevance to query
            scored = [
                (s, self._sentence_relevance(s, query))
                for s in sentences
            ]
            top = sorted(scored, key=lambda x: x[1], reverse=True)[0]
            key_sentences.append(top[0])

        # Generate from key sentences
        prompt = f"""
        Based on these key rules:
        {". ".join(key_sentences)}

        Answer this question: {query}
        """
        return await self.llm.generate(prompt)

    async def path_ambiguous(self, query: str, docs: list[Document]) -> str:
        """Augment with web search"""

        web_results = await self.web_search(query)
        combined = docs + web_results[:3]

        prompt = f"""
        Synthesize these sources to answer: {query}

        Internal sources: {self.format_docs(docs)}
        Web sources: {self.format_docs(web_results)}

        Note: Internal sources may conflict with web; cite both.
        """
        return await self.llm.generate(prompt)

    async def path_incorrect(self, query: str) -> str:
        """Retrieve from web only"""

        web_results = await self.web_search(query)

        prompt = f"""
        Answer this question using web sources only:
        {query}

        Web sources: {self.format_docs(web_results)}
        """
        return await self.llm.generate(prompt)

    def _sentence_relevance(self, sentence: str, query: str) -> float:
        """Score sentence relevance to query (TF-IDF or cosine)"""
        # Simplified: count matching words
        query_words = set(query.lower().split())
        sentence_words = set(sentence.lower().split())
        overlap = len(query_words & sentence_words)
        return overlap / max(len(query_words), 1)
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Accuracy** | +12% (self-correcting) | Complex routing logic |
| **Cost** | $0.010 (better than Advanced) | Multiple paths to manage |
| **Reliability** | Evaluator prevents hallucinations | T5 evaluator required |
| **Flexibility** | Adaptive (uses web when needed) | Depends on web search |

---

## Integration

**Tier Level**: BALANCED tier (best for rule lookups)

**Evaluator Model**: T5-large or similar (NOT LLM)

**Research**:
- [Corrective RAG Implementation](https://www.datacamp.com/tutorial/corrective-rag-crag)
- [LangGraph CRAG Tutorial](https://langchain-ai.github.io/langgraph/tutorials/rag/langgraph_crag/)

---

**Status**: Production-Ready | **MeepleAI Tier**: P1 Rule Lookup
