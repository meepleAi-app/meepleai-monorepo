# Modular RAG

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | Variable (2,000–15,000) |
| **Cost/Query** | Variable (depends on flow) |
| **Accuracy** | Variable (+5-20%) |
| **Latency** | Variable (100ms–10s) |
| **Priority** | **P0** - Framework |

---

## Architecture Diagram

```
Query Input
    ↓
[Modular Components]
    ├─ Router: Decides which path
    ├─ Retriever: Fetches documents
    ├─ Grader: Evaluates relevance
    ├─ Generator: Creates answer
    └─ Checker: Validates output
    ↓
[Flow Pattern Selection]
    ├─ Linear: Simple path (no branching)
    ├─ Conditional: Routes based on conditions
    ├─ Looping: Iterate until confident
    └─ Branching: Multi-agent paths
    ↓
Answer (flow-specific)
```

---

## How It Works

**Modular Philosophy**:
- Each component has ONE job
- Components reusable in different flows
- Flow pattern (linear/conditional/looping) determines token usage

**Flow Patterns**:

1. **Linear** (FAST, 2,000 tokens):
   ```
   Router → Retriever (3 chunks) → Generator → Answer
   ```

2. **Conditional** (BALANCED, 3,570 tokens):
   ```
   Router → Retriever (10) → Grader → IF (correct) → Generator
                                      → IF (incorrect) → Web search
   ```

3. **Looping** (ITERATIVE, 7,000 tokens):
   ```
   Router → Retriever → Generator → Evaluator
              ↑ (re-retrieve if low confidence)
   ```

4. **Branching** (MULTI-AGENT, 9,650 tokens):
   ```
   Router → Retriever → [Agent 1, Agent 2, Agent 3] → Judge
   ```

---

## Token Breakdown

**By Flow Pattern**:
- Linear: ~2,000 tokens
- Conditional: ~3,570 tokens
- Looping (2 iter): ~7,000 tokens
- Branching (3 agents): ~9,650 tokens

---

## When to Use

✅ **Best For**:
- Organizations wanting flexibility
- Mixing strategies per query type
- Evolving requirements
- Research/experimentation

✅ **Advantages**:
- Reusable components
- Swap strategies without rewriting
- Mix patterns in same system

---

## Code Example

```python
class ModularRag:
    """LEGO-like RAG components"""

    async def execute_linear_flow(
        self,
        query: str
    ) -> str:
        """Linear: Simplest flow"""

        # Router (intent detection only, no branching)
        template = await self.router.classify(query)

        # Retriever (top-3 only)
        docs = await self.retriever.fetch(query, top_k=3)

        # Generator (direct)
        answer = await self.generator.synthesize(query, docs)

        return answer

    async def execute_conditional_flow(
        self,
        query: str
    ) -> str:
        """Conditional: Route based on evaluation"""

        # Router
        template = await self.router.classify(query)

        # Retriever
        docs = await self.retriever.fetch(query, top_k=10)

        # Grader (evaluate docs)
        evaluation = await self.grader.evaluate(docs, query)

        # Conditional routing
        if evaluation.correctness > 0.8:
            # Path A: Use internal docs
            answer = await self.generator.synthesize(query, docs)
        elif evaluation.correctness > 0.5:
            # Path B: Augment with web search
            web_docs = await self.web_search(query)
            answer = await self.generator.synthesize(
                query,
                docs + web_docs
            )
        else:
            # Path C: Web search only
            web_docs = await self.web_search(query)
            answer = await self.generator.synthesize(query, web_docs)

        return answer

    async def execute_looping_flow(
        self,
        query: str,
        max_iterations: int = 3
    ) -> str:
        """Looping: Iterate until confident"""

        for iteration in range(max_iterations):
            # Retrieve
            docs = await self.retriever.fetch(query, top_k=5)

            # Generate
            answer = await self.generator.synthesize(query, docs)

            # Evaluate confidence
            confidence = await self.evaluator.assess_confidence(
                answer,
                docs
            )

            if confidence > 0.85:
                return answer

            # Refine query for next iteration
            query = await self.refiner.refine(query, answer, confidence)

        return answer

    async def execute_branching_flow(
        self,
        query: str
    ) -> str:
        """Branching: Multi-agent collaboration"""

        # Retrieve (shared)
        docs = await self.retriever.fetch(query, top_k=10)

        # Parallel agent execution
        results = await asyncio.gather(
            self.analyzer_agent.analyze(query, docs),
            self.strategist_agent.strategize(query, docs),
            self.validator_agent.validate(query, docs)
        )

        # Judge agent reconciles
        answer = await self.judge_agent.judge(
            results,
            query,
            docs
        )

        return answer
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Flexibility** | Mix patterns dynamically | Complex orchestration |
| **Reusability** | Components pluggable | Requires framework thinking |
| **Optimization** | Match pattern to query type | Learning curve |
| **Cost** | Variable (optimize per type) | Multiple paths to maintain |

---

## Integration

**Tier Level**: Foundation (all tiers use modular approach)

**Flow Selection**:
- Simple queries → Linear
- Ambiguous → Conditional
- Iterative refinement → Looping
- Complex strategic → Branching

---

## Research Sources

- [Modular RAG: LEGO-like Reconfigurable Frameworks](https://arxiv.org/html/2407.21059v1)

---

**Status**: Production-Ready | **MeepleAI Tier**: P0 Framework
