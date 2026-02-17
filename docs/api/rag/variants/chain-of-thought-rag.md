# Chain-of-Thought RAG

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 3,650 |
| **Cost/Query** | $0.016 |
| **Accuracy** | +18% above naive |
| **Latency** | 1–2s |
| **Priority** | **P1** - Strategic Queries |

---

## Architecture Diagram

```
Query Input
    ↓
[Retrieval] Standard vector search (top-5)
    ↓
[CoT Generation] Multi-step reasoning
    ├─ Thought 1: Analyze requirements
    ├─ Thought 2: Extract relevant rules
    ├─ Thought 3: Evaluate options
    └─ Thought 4: Generate recommendation
    ↓
[Structured Output]
    ├─ Reasoning steps (explanation)
    └─ Final answer (conclusion)
    ↓
Answer (with transparent reasoning)
```

---

## How It Works

1. **Retrieve**: Standard top-5 chunks (2,500 tokens)
2. **CoT Prompt**: Explicitly request step-by-step reasoning
3. **Multi-Step Output**: Generate extended reasoning
   - Thought 1-3: Intermediate steps (200-400 tokens)
   - Final: Recommendation (100-200 tokens)
4. **Transparency**: Return reasoning trace to user
5. **Format**: JSON with `reasoning_steps` + `final_answer`

---

## Token Breakdown

**Retrieval**:
- Vector search: 2,500 tokens (5 chunks)

**Generation**:
- Input: 500 (CoT prompt) + 50 (query) + 2,500 (docs) = 3,050 tokens
- Output: 600 tokens (400 reasoning + 200 answer)

**Total**: 3,650 tokens

**Cost** (Claude Sonnet): $0.016 per query

---

## When to Use

✅ **Best For**:
- Strategic planning queries ("should I...")
- Trade-off analysis
- When user wants to see reasoning
- Learning/educational context

❌ **Not For**:
- Simple fact lookups
- Token-constrained environments

---

## Code Example

```python
class CoTRag:
    async def generate_with_cot(self, query: str, docs: list[Document]) -> dict:
        """Generate answer with chain-of-thought reasoning"""

        cot_prompt = f"""
        You are analyzing a board game rules question with detailed reasoning.

        Retrieved Rules:
        {self.format_docs(docs)}

        Question: {query}

        Think through this step-by-step:
        1. What is the core question being asked?
        2. Which rules are directly relevant?
        3. What are the different interpretations or options?
        4. What is the most logical conclusion?

        Respond in JSON format:
        {{
            "reasoning_steps": [
                "Step 1: ...",
                "Step 2: ...",
                "Step 3: ...",
                "Step 4: ..."
            ],
            "final_answer": "...",
            "confidence": 0.0-1.0,
            "citations": ["page X", "page Y"]
        }}
        """

        response = await self.llm.generate(cot_prompt)
        return json.loads(response)

    def format_docs(self, docs: list[Document]) -> str:
        """Format docs with page citations"""
        formatted = []
        for doc in docs:
            formatted.append(
                f"Rule {doc.id} (page {doc.metadata['page']}): {doc.content}"
            )
        return "\n\n".join(formatted)
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Reasoning** | Transparent (user sees steps) | +200 output tokens |
| **Accuracy** | +18% improvement | Longer answers |
| **Implementation** | Simple (just better prompt) | Requires JSON parsing |
| **Debugging** | Easy to trace reasoning | More verbose |

---

## Integration

**Tier Level**: BALANCED tier (resource_planning template only)

**When to Use**:
- Resource_planning queries where user benefits from reasoning
- Strategic queries requiring transparency
- Educational/learning context

**When Not**:
- rule_lookup template (too verbose)
- FAST tier (use simpler prompts)

---

## Research Sources

- [Chain-of-Thought Prompting Elicits Reasoning in Large Language Models](https://arxiv.org/abs/2201.11903)
- [Standard RAG practice applying CoT to retrieval-based generation](https://www.promptingguide.ai/techniques/cot)

---

**Status**: Production-Ready | **MeepleAI Tier**: P1 Strategic
