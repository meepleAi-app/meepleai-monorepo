# Few-Shot RAG

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 4,150 |
| **Cost/Query** | $0.016 |
| **Accuracy** | +5% above naive |
| **Latency** | 500ms–1s |
| **Priority** | **P2** - Format Control |

---

## Architecture Diagram

```
Query Input
    ↓
[Few-Shot Examples] Fixed (900 tokens)
    ├─ 3 examples of ideal answers
    ├─ Format: Question → Answer with citations
    └─ Cost: Cached (Anthropic prompt caching)
    ↓
[Retrieval] Standard (2,500 tokens)
    ↓
[Generation] Following example format
    ├─ Input: 3,850 tokens (900 examples + 2,950 docs)
    └─ Output: 300 tokens
    ↓
Answer (consistent format)
```

---

## How It Works

1. **Few-Shot Examples**: 3-5 high-quality examples
   - Format: `{"question": "...", "answer": "...", "citations": [...]}`
   - Cost: 900 tokens (fixed)
2. **Prompt Caching**: Cache examples for reuse
   - With caching: 90% discount on cached tokens
   - Cost: 90 tokens effective (vs 900 without caching)
3. **Retrieval**: Standard top-5
4. **Generation**: Model follows example format

---

## Token Breakdown

**Few-Shot Examples** (without caching):
- 3 examples @ 300 tokens each = 900 tokens

**With Anthropic Prompt Caching**:
- Cached tokens: 900 × 0.1 = 90 tokens effective

**Retrieval**:
- 2,500 tokens

**Generation**:
- Input: 400 + 900 (examples) + 50 + 2,500 = 3,850 tokens
- Output: 300 tokens

**Total (with caching)**: 90 + 2,500 + 3,850 + 300 = **6,740 tokens**
**Total (without caching)**: 900 + 2,500 + 3,850 + 300 = **7,550 tokens**

**Cost**: $0.016–$0.020 per query

---

## When to Use

✅ **Best For**:
- Format consistency critical
- Teaching model output structure
- Answer citation format
- Q&A pair consistency

❌ **Not For**:
- Content, only format needed

---

## Code Example

```python
class FewShotRag:
    FEW_SHOT_EXAMPLES = """
    Example 1:
    Question: How many resources do you start with in Catan?
    Answer: According to the rulebook, each player starts with 2 settlements and 2 roads (page 4). No initial resources are distributed; players collect resources based on their settlement placements during initial placement phase.
    Citations: page 4

    Example 2:
    Question: Can you trade resources with other players?
    Answer: Yes, players may trade resources freely with other players at any time during their turn, or make bank trades (4:1 ratio) if no player accepts their offer (page 9).
    Citations: page 9

    Example 3:
    Question: What happens when the robber is placed on your hex?
    Answer: When the robber occupies a hex where you have a settlement or city, you cannot collect resources from that hex until the robber moves (page 12). Optionally, the player placing the robber may steal one random card from your hand.
    Citations: page 12
    """

    async def generate_with_few_shot(
        self,
        query: str,
        docs: list[Document]
    ) -> str:
        """Generate with few-shot examples for consistent format"""

        prompt = f"""
        You are a board game rules expert. Provide answers in this format:
        1. Direct answer with relevant rule text
        2. Page citations
        3. Clarify any ambiguities

        {self.FEW_SHOT_EXAMPLES}

        Now answer this question:
        Question: {query}

        Retrieved Rules:
        {self.format_docs(docs)}

        Response (follow example format):
        """

        return await self.llm.generate(
            prompt,
            use_prompt_caching=True  # Cache the examples
        )
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Format** | Consistent output format | +900 base tokens |
| **Quality** | Teaches model by example | Overhead for simple queries |
| **Cost** | $0.016/query with caching | Requires good examples |
| **Caching** | 90% discount with prompt caching | Caching requires Anthropic API |

---

## Integration

**Tier Level**: BALANCED tier (optional enhancement)

**With Prompt Caching**:
- First query: 900 tokens (cache write)
- Subsequent queries: 90 tokens (cache hit)
- **30% reduction over time**

---

## Research Sources

- [Few-Shot Prompting Guide](https://www.promptingguide.ai/techniques/fewshot)
- [Anthropic Prompt Caching](https://docs.anthropic.com/claude/reference/prompt-caching)

---

**Status**: Production-Ready | **MeepleAI Tier**: P2 Format Control
