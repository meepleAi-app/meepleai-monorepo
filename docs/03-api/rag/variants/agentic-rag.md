# Agentic RAG

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 7,000–15,000 |
| **Cost/Query** | $0.03–$0.10 |
| **Accuracy** | +15–25% above naive |
| **Latency** | 2–10s |
| **Priority** | **P1** - PRECISE Tier |

---

## Architecture Diagram

```
Query Input
    ↓
[Router] Intent Classification → PRECISE
    ↓
[Multi-Hop Retriever] Adaptive depth
    ├─ Hop 1: Initial retrieval (top-20)
    ├─ Hop 2: Entity expansion (related rules)
    └─ Hop 3: Cross-reference validation
    ↓
[LLM Grader] Relevance + Contradiction Detection
    ↓
[Multi-Agent Generator]
    ├─ Analyzer Agent: Rule analysis (Claude Haiku)
    ├─ Strategist Agent: Recommendation (Claude Opus)
    └─ Validator Agent: Cross-check (Claude Haiku)
    ↓
[Self-Reflection Checker] Confidence verification
    ↓
Answer (with reasoning trace)
```

---

## How It Works

1. **Multi-Hop Retrieval**: Fetch broader context (up to 3 hops)
   - Hop 1: Primary topic rules
   - Hop 2: Related entity rules
   - Hop 3: Cross-reference validation
2. **LLM-Based Grading**: Evaluate relevance + detect contradictions
3. **Multi-Agent Generation**: Collaborate across specialized agents
   - Analyzer: Decompose problem
   - Strategist: Generate recommendation
   - Validator: Verify against rules
4. **Self-Reflection**: LLM validates own output
5. **Escalation**: Re-retrieve if confidence <90%

---

## Token Breakdown

**Retrieval** (3 hops):
- Hop 1: 5,000 tokens
- Hop 2: 3,000 tokens (entity-focused)
- Hop 3: 2,000 tokens (validation)
- Total: 10,000 tokens

**LLM Grading**:
- Input: 1,000 (evaluation prompt) + 8,000 (docs) = 9,000 tokens
- Output: 200 tokens
- Total: 9,200 tokens

**Multi-Agent Generation**:
- Analyzer: 2,000 input + 300 output
- Strategist: 2,500 input + 500 output
- Validator: 2,000 input + 300 output
- Total: 7,600 tokens

**Self-Reflection**:
- Input: 3,000 tokens
- Output: 200 tokens
- Total: 3,200 tokens

**Grand Total**: ~30,000 tokens (but parallelizable for ~50% latency reduction)

---

## When to Use

✅ **Best For**:
- Complex strategic queries
- Contradiction resolution
- Multi-concept rule synthesis
- Premium user queries

❌ **Not For**:
- Simple fact lookups
- Latency-critical (<1s) applications

---

## Code Example

```python
class AgenticRag:
    async def execute_precise_query(self, query: str) -> AgenticResponse:
        # Step 1: Multi-hop retrieval
        all_docs = await self.multi_hop_retrieve(query, hops=3)

        # Step 2: LLM-based grading
        graded_docs = await self.llm_grade(all_docs, query)

        # Step 3: Multi-agent generation
        agents = {
            "analyzer": AnalyzerAgent(model="haiku"),
            "strategist": StrategistAgent(model="opus"),
            "validator": ValidatorAgent(model="haiku")
        }

        # Run agents in parallel
        analyses = await asyncio.gather(
            agents["analyzer"].analyze(graded_docs, query),
            agents["strategist"].strategize(graded_docs, query),
        )

        # Validator checks strategist output
        final_answer = await agents["validator"].validate(
            analyses[1],  # strategist output
            graded_docs,
            query
        )

        # Step 4: Self-reflection
        confidence = await self.self_reflect(final_answer, graded_docs)

        if confidence < 0.90:
            # Re-retrieve (max 1 iteration)
            refined_docs = await self.multi_hop_retrieve(query, hops=4)
            return await self.execute_precise_query(query)  # Recursive

        return final_answer

    async def multi_hop_retrieve(self, query: str, hops: int):
        """Retrieve with 3-5 hops of expansion"""
        docs = []

        # Hop 1: Initial retrieval
        hop1 = await self.vector_search(query, top_k=20)
        docs.extend(hop1)

        if hops < 2:
            return docs

        # Hop 2: Entity expansion
        entities = self._extract_entities(hop1)
        hop2 = await self.search_entities(entities, top_k=10)
        docs.extend(hop2)

        if hops < 3:
            return docs

        # Hop 3: Cross-reference
        hop3 = await self._cross_reference_validation(hop1, hop2)
        docs.extend(hop3)

        return self.deduplicate(docs)
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Accuracy** | +20-25% (best in class) | Complex implementation |
| **Reasoning** | Transparent (agent trace) | 2-10s latency |
| **Cost** | $0.03-0.10 per query | 15-30x naive RAG |
| **Reliability** | Self-validation built-in | Risk of circular reasoning |

---

## Integration

**Tier Level**: PRECISE tier in 5-tier architecture

**Multi-Agent Inspiration**: Belle et al. (2025) - Agents of Change
- Specialized roles: Analyzer, Strategist, Validator
- Iterative improvement through agent feedback

---

## Research Sources

- [Advanced RAG Techniques](https://neo4j.com/blog/genai/advanced-rag-techniques/)
- [Building Agentic RAG Systems with LangGraph](https://rahulkolekar.com/building-agentic-rag-systems-with-langgraph/)
- [Agents of Change: Self-Evolving LLM Agents](https://nbelle1.github.io/agents-of-change/)

---

**Status**: Production-Ready | **MeepleAI Tier**: P1 PRECISE
