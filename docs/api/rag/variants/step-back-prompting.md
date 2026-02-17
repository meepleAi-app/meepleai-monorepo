# Step-Back Prompting

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 5,740 |
| **Cost/Query** | $0.022 |
| **Accuracy** | +10% above naive |
| **Latency** | 1–3s |
| **Priority** | **P2** - Conceptual |

---

## Architecture Diagram

```
Query Input: "How do food tokens interact with bird placement?"
    ↓
[Step-Back Generation] Generate abstract question
    ├─ Input: 250 (prompt) + 50 (query) = 300 tokens
    ├─ Output: 40 tokens ("What are the general resource mechanics?")
    ↓
[Dual Retrieval]
    ├─ Hop 1 (Background): Retrieve conceptual context (2,000 tokens)
    │   └─ "How do resources work?" → general rules
    ├─ Hop 2 (Specific): Retrieve specific answer (2,500 tokens)
    │   └─ Original query → detailed rules
    ↓
[Generation] With both contexts
    ├─ Input: 500 + 50 + 2,000 + 2,500 = 5,050 tokens
    └─ Output: 350 tokens
    ↓
Answer (with conceptual grounding)
```

---

## How It Works

1. **Step-Back Generation** (LLM):
   - Create abstraction of original query
   - Find broader concept
   - Cost: 300 input + 40 output = 340 tokens

2. **Dual Retrieval**:
   - Background: Retrieve broader context (2,000 tokens)
   - Specific: Retrieve specific answer (2,500 tokens)

3. **Generation**:
   - Synthesize from both levels
   - Provides conceptual grounding + specific details

---

## Token Breakdown

**Step-Back Generation**:
- Input: 250 + 50 = 300 tokens
- Output: 40 tokens

**Background Retrieval** (broader concept):
- 2,000 tokens (4 chunks)

**Specific Retrieval** (original query):
- 2,500 tokens (5 chunks)

**Generation**:
- Input: 500 + 50 + 2,000 + 2,500 = 5,050 tokens
- Output: 350 tokens

**Total**: 300 + 40 + 2,000 + 2,500 + 5,050 + 350 = **10,240 tokens**

(Optimized: ~5,740 with caching)

---

## When to Use

✅ **Best For**:
- Queries needing conceptual background
- Understanding before specific answer
- "Why" questions

❌ **Not For**:
- Simple fact lookups
- Time-critical

---

## Code Example

```python
class StepBackPrompting:
    async def step_back_and_retrieve(
        self,
        query: str
    ) -> tuple[list[Document], list[Document]]:
        """Retrieve background + specific using step-back"""

        # Step 1: Generate step-back question
        step_back = await self.generate_step_back(query)

        # Step 2: Parallel retrieval
        background, specific = await asyncio.gather(
            self.retrieve(step_back, top_k=4),
            self.retrieve(query, top_k=5)
        )

        return background, specific

    async def generate_step_back(self, query: str) -> str:
        """Generate broader concept question"""

        prompt = f"""
        What is the more general concept behind this question?
        {query}

        Return a broader question (one sentence):
        """

        return await self.llm.generate(prompt)

    async def synthesize_with_background(
        self,
        query: str,
        background_docs: list[Document],
        specific_docs: list[Document]
    ) -> str:
        """Synthesize using both levels of context"""

        prompt = f"""
        First, understand the conceptual background:
        {self.format_docs(background_docs)}

        Then, answer specifically:
        {query}

        Specific rules:
        {self.format_docs(specific_docs)}

        Provide answer that builds from general concept to specific.
        """

        return await self.llm.generate(prompt)
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Understanding** | Conceptual grounding | Extra LLM call for step-back |
| **Accuracy** | +10% improvement | Dual retrieval |
| **Use Case** | Great for "why" questions | More tokens (2.9x) |

---

## Integration

**Tier Level**: BALANCED tier (conceptual queries)

**Optimization**: Cache step-back questions for common topics

---

## Research Sources

- [Step-Back Prompting for Improved Reasoning](https://arxiv.org/abs/2310.06117)
- [Query Transformations](https://hub.athina.ai/research-papers/query-transformations-rewriting-step-back-prompting-and-sub-query-decomposition/)

---

**Status**: Production-Ready | **MeepleAI Tier**: P2 Conceptual
