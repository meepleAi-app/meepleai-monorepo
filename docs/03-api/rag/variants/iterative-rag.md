# Iterative RAG

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 6,736 (avg 2.3 iterations) |
| **Cost/Query** | $0.025 |
| **Accuracy** | +14% above naive |
| **Latency** | 2–5s |
| **Priority** | **P1** - Ambiguous Queries |

---

## Architecture Diagram

```
Query Input
    ↓
[Iteration 1]
    ├─ Retrieve + Generate: 3,250 tokens
    ├─ Evaluate confidence
    └─ If confident, done
    ↓
[Iteration 2] (70% of queries need it)
    ├─ Evaluate → Refine query
    ├─ Re-retrieve: 3,460 tokens
    ├─ Re-generate: 350 tokens
    ↓
[Iteration 3] (20% of queries)
    ├─ Another cycle if needed
    ↓
Answer (after N iterations)
```

---

## How It Works

1. **Iteration 1**: Generate initial answer
2. **Evaluation**:
   - Calculate confidence score (0-1)
   - Refine query based on retrieved docs
   - Identify gaps
3. **Iteration 2** (if needed, 70% of queries):
   - Re-retrieve with refined query
   - Re-generate answer
4. **Iteration 3** (if needed, 20% of queries):
   - Last attempt before giving up
5. **Max Iterations**: 2-3 (prevent infinite loops)

---

## Token Breakdown

**Iteration 1** (100% of queries):
- Retrieve: 2,500 tokens
- Generate: 400 input + 300 output = 700 tokens
- Evaluate: 300 input + 100 output = 400 tokens
- Total: 3,600 tokens

**Iteration 2** (70% of queries):
- Re-retrieve: 3,000 tokens (refined query)
- Re-generate: 400 input + 350 output = 750 tokens
- Total: 3,750 tokens

**Iteration 3** (20% of queries):
- Same as Iteration 2: 3,750 tokens

**Weighted Average**:
- 1 iter (30%): 3,600 tokens
- 2 iter (50%): 3,600 + 3,750 = 7,350 tokens
- 3 iter (20%): 3,600 + 3,750 + 3,750 = 11,100 tokens
- **Average**: 0.3×3,600 + 0.5×7,350 + 0.2×11,100 = **6,735 tokens**

---

## When to Use

✅ **Best For**:
- Ambiguous queries (needs refinement)
- Complex questions (multiple retrieval passes)
- When user doesn't find answer in first try

❌ **Not For**:
- Simple lookups
- Latency-critical (<1s)

---

## Code Example

```python
class IterativeRag:
    async def execute_iterative(
        self,
        query: str,
        max_iterations: int = 3
    ) -> IterativeResponse:
        """Iterative RAG with evaluation"""

        current_query = query
        all_docs = []
        iteration = 0

        while iteration < max_iterations:
            iteration += 1

            # Retrieve
            docs = await self.retrieve(current_query, top_k=5)
            all_docs.extend(docs)

            # Generate
            answer = await self.generate(current_query, docs)

            # Evaluate confidence
            evaluation = await self.evaluate_confidence(
                answer,
                docs,
                current_query
            )

            if evaluation.confidence > 0.85:
                # Confident, return answer
                return IterativeResponse(
                    answer=answer,
                    iterations=iteration,
                    confidence=evaluation.confidence
                )

            if iteration >= max_iterations:
                # Last iteration, return best-effort answer
                return IterativeResponse(
                    answer=answer,
                    iterations=iteration,
                    confidence=evaluation.confidence
                )

            # Refine query for next iteration
            current_query = await self.refine_query(
                current_query,
                answer,
                evaluation
            )

        return IterativeResponse(
            answer=answer,
            iterations=iteration,
            confidence=evaluation.confidence
        )

    async def evaluate_confidence(
        self,
        answer: str,
        docs: list[Document],
        query: str
    ) -> EvaluationResult:
        """Evaluate answer confidence"""

        prompt = f"""
        Evaluate confidence of this answer:

        Question: {query}
        Answer: {answer}
        Retrieved docs: {self.format_docs(docs)}

        Score 0-1: Is answer well-supported?
        If not, what refined question should we ask?
        """

        result = await self.llm.generate(prompt)
        return EvaluationResult(
            confidence=result.confidence,
            refined_query=result.refined_query
        )

    async def refine_query(
        self,
        original: str,
        answer: str,
        evaluation: EvaluationResult
    ) -> str:
        """Refine query for next iteration"""

        prompt = f"""
        Original question: {original}
        First attempt answer: {answer}
        Problem: {evaluation.feedback}

        Generate refined question for next retrieval:
        """

        return await self.llm.generate(prompt)
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Accuracy** | +14% improvement | Multiple LLM calls (6.7x tokens) |
| **Latency** | 2-5s (acceptable) | Slower than single-pass |
| **Flexibility** | Handles ambiguous queries | Requires evaluation logic |
| **Cost** | $0.025 (reasonable for gain) | 3-4x naive RAG |

---

## Integration

**Tier Level**: BALANCED/PRECISE tier (for ambiguous queries)

**Configuration**:
- Max iterations: 2-3 (prevent loops)
- Confidence threshold: 0.85
- Use Haiku for evaluation (cheaper)

---

## Research Sources

- [Modular RAG - Iterative Retrieval](https://arxiv.org/html/2407.21059v1)

---

**Status**: Production-Ready | **MeepleAI Tier**: P1 Ambiguous
