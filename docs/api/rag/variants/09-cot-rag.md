# Chain-of-Thought RAG (CoT-RAG)

**Stats**: 3,650 tokens/query | $0.016/query | +18% accuracy | 1-2s latency | **Priority: P1**

## Architecture

```
Query → Retrieve Context (2,500 tokens)
    ↓
┌─────────────────────────────────────────┐
│ CoT Generation with Explicit Reasoning  │
│ Prompt: "Think step-by-step"            │
│ Output: Reasoning (400) + Answer (200)  │
└─────────────────────────────────────────┘
    ↓
Total: 3,650 tokens (600 output vs 300 standard)
```

## How It Works

Chain-of-Thought RAG extends standard RAG by prompting the LLM to generate explicit reasoning steps before producing the final answer. The system instruction includes "Think step-by-step" or "Explain your reasoning", causing the model to output intermediate logical steps (reasoning trace) followed by the answer.

This increases output tokens significantly (400 reasoning + 200 answer = 600 total vs 300 standard), raising per-query costs by ~$0.005. However, CoT provides two critical benefits: (1) +15-20% accuracy on complex reasoning tasks, and (2) transparent reasoning that users can verify and trust.

For board game rules, CoT is particularly valuable for strategic planning queries: "What's the best opening strategy?" benefits from seeing the reasoning ("First, consider resource generation... Second, evaluate turn order advantages...") rather than just a declarative answer. Users can verify the logic against their game knowledge.

The reasoning trace also improves debugging: if the answer is wrong, the reasoning reveals whether the error was retrieval (wrong chunks), logic (flawed reasoning), or generation (misinterpretation).

## Token Breakdown

**Retrieval**:
- Standard retrieval: 2,500 tokens (5 chunks)

**CoT Generation**:
- Input: 500 (CoT system prompt) + 50 (query) + 2,500 (context) = 3,050 tokens
- Output: 400 (reasoning steps) + 200 (final answer) = 600 tokens
- **vs Standard**: 300 output tokens

**Total**: 3,050 (input) + 600 (output) = **3,650 tokens**

**Cost Increase**: Output tokens cost 5x input ($15/1M vs $3/1M for Sonnet), so +300 output tokens = +$0.0045 per query

## When to Use

- **Strategic/tactical queries** requiring multi-step reasoning (e.g., "optimal turn 1 strategy")
- **PRECISE tier** where reasoning transparency is valued (Editor/Admin users)
- **Debugging/validation** scenarios where understanding the model's logic is critical

## Code Example

```python
from anthropic import Anthropic

client = Anthropic(api_key="...")

def cot_rag(query: str) -> dict:
    """RAG with Chain-of-Thought reasoning for complex queries."""

    # Retrieve context
    query_embedding = get_embedding(query)
    chunks = vector_search(query_embedding, top_k=5)
    context = "\n\n".join([c['text'] for c in chunks])

    # Generate with CoT prompting
    response = client.messages.create(
        model="claude-3-5-sonnet-20241022",
        max_tokens=600,  # Increased for reasoning + answer
        messages=[{
            "role": "user",
            "content": f"""Question: {query}

Context from rulebook:
{context}

Instructions:
1. Think step-by-step through the relevant rules
2. Explain your reasoning explicitly
3. Provide the final answer

Format your response as:

**Reasoning:**
[Your step-by-step analysis]

**Answer:**
[Concise final answer]
"""
        }]
    )

    # Parse reasoning and answer
    content = response.content[0].text
    parts = content.split("**Answer:**")

    return {
        "reasoning": parts[0].replace("**Reasoning:**", "").strip(),
        "answer": parts[1].strip() if len(parts) > 1 else content,
        "tokens": {
            "input": response.usage.input_tokens,
            "output": response.usage.output_tokens
        }
    }

# Example usage
result = cot_rag("What's the optimal opening strategy in Wingspan?")
print("Reasoning:", result['reasoning'])
print("\nAnswer:", result['answer'])
```

## Integration

CoT-RAG integrates at **Layer 5 (Generation)** in TOMAC-RAG, modifying the prompt structure and output parsing.

**Standard Generation**:
```
Prompt: "Answer: {query}\n\nContext: {chunks}"
Output: Direct answer (300 tokens)
```

**CoT Generation** (THIS LAYER):
```
Prompt: "Think step-by-step... Explain reasoning... Format: Reasoning + Answer"
Output: Reasoning trace (400 tokens) + Final answer (200 tokens)
```

**Flow Integration**:
1. **Layer 1-4**: Route → Cache → Retrieve → CRAG (same as standard)
2. **Layer 5**: CoT generation with reasoning trace (MODIFIED)
3. **Layer 6**: Validate both reasoning logic AND final answer

**User Experience**:
- Show reasoning in collapsible section (default hidden)
- Highlight answer prominently
- Allow users to verify reasoning against rulebook citations

## Sources

- [Chain-of-Thought Prompting](https://arxiv.org/abs/2201.11903)
- [RAG with CoT for Complex Reasoning](https://www.promptingguide.ai/techniques/cot)
