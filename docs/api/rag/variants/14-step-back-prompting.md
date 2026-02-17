# Step-Back Prompting RAG

**Stats**: 5,740 tokens/query | $0.022/query | +10% accuracy | 1-3s latency | **Priority: P2**

## Architecture

```
User Query: "Can I attack diagonally in Wingspan?"
    ↓
┌────────────────────────────────────────┐
│ Step-Back LLM Call                     │
│ "What's the broader concept?"          │
│ Output: "What are the general combat   │
│          rules in Wingspan?"           │
└────────────────────────────────────────┘
    ↓
Dual Retrieval:
├─ Background: General combat rules (2,000 tokens)
└─ Specific: Diagonal attack rules (2,500 tokens)
    ↓
Generate with Both Contexts (5,050 tokens)
```

## How It Works

Step-Back Prompting generates an abstract, higher-level version of the query to retrieve conceptual background before addressing the specific question. This two-stage retrieval (general principles → specific details) improves reasoning by grounding answers in foundational concepts.

The workflow: (1) LLM generates a "step-back" query by abstracting the specific question to a broader concept, (2) retrieve background context using the step-back query (general principles, overviews), (3) retrieve specific context using the original query (precise details), (4) generate answer using both background and specific context, allowing the LLM to reason from principles to specifics.

For example, "Can I attack diagonally in Wingspan?" steps back to "What are the general movement/attack rules in Wingspan?" The background retrieval provides foundational understanding (e.g., "Wingspan is not a combat game"), which prevents hallucinating combat rules that don't exist. The specific retrieval confirms there are no diagonal attack mechanics.

This is particularly valuable for queries about edge cases, exceptions, or novel combinations where reasoning from first principles prevents incorrect inferences.

## Token Breakdown

**Step-Back Generation**:
- Input: 250 (prompt: "What's the broader concept?") + 50 (specific query) = 300 tokens
- Output: 40 tokens (abstract query)

**Dual Retrieval**:
- Background retrieval (step-back query): 2,000 tokens (4 broader context chunks)
- Specific retrieval (original query): 2,500 tokens (5 specific chunks)
- **Total context**: 4,500 tokens

**Generation** (with both contexts):
- Input: 500 (enhanced prompt) + 50 (query) + 2,000 (background) + 2,500 (specific) = 5,050 tokens
- Output: 350 tokens

**Total Input**: 300 + 5,050 = 5,350 tokens
**Total Output**: 40 + 350 = 390 tokens
**Grand Total**: **5,740 tokens**

## When to Use

- **Edge case queries** requiring conceptual grounding (e.g., "Can I do X?" where X is unusual)
- **Ambiguous questions** benefiting from foundational context before specific details
- **Educational contexts** where reasoning process matters (show principles → application)

## Code Example

```python
from anthropic import Anthropic

client = Anthropic(api_key="...")

def step_back_rag(query: str) -> str:
    """RAG with step-back prompting for conceptual grounding."""

    # Step 1: Generate step-back (abstract) query
    step_back_response = client.messages.create(
        model="claude-3-5-haiku-20241022",
        max_tokens=100,
        messages=[{
            "role": "user",
            "content": f"""Given this specific question, generate a broader, more abstract question about the underlying concept or principle.

Specific question: {query}

Broader question: """
        }]
    )
    step_back_query = step_back_response.content[0].text.strip()

    # Step 2: Dual retrieval
    # Background context (step-back query)
    background_embedding = get_embedding(step_back_query)
    background_chunks = vector_search(background_embedding, top_k=4)
    background_context = "\n\n".join([c['text'] for c in background_chunks])

    # Specific context (original query)
    specific_embedding = get_embedding(query)
    specific_chunks = vector_search(specific_embedding, top_k=5)
    specific_context = "\n\n".join([c['text'] for c in specific_chunks])

    # Step 3: Generate with both contexts
    response = client.messages.create(
        model="claude-3-5-sonnet-20241022",
        max_tokens=350,
        messages=[{
            "role": "user",
            "content": f"""Answer this specific question using both background principles and specific details.

Specific Question: {query}
Broader Context: {step_back_query}

Background Information (general principles):
{background_context}

Specific Information (detailed rules):
{specific_context}

Instructions:
1. First, establish relevant general principles
2. Then, apply them to answer the specific question
3. Provide a clear, well-reasoned answer

Answer:"""
        }]
    )

    return response.content[0].text

# Example usage
answer = step_back_rag("Can I attack diagonally in Wingspan?")
# Expected: "Wingspan is a bird collection game without combat mechanics.
#            There are no attack actions, diagonal or otherwise."
```

## Integration

Step-Back Prompting integrates as a **pre-retrieval enhancement** in TOMAC-RAG, generating an abstract query before dual retrieval.

**Enhanced Flow**:
1. **Layer 1 (Routing)**: Detect edge case or conceptual query
2. **Layer 2.5** (NEW): Generate step-back query (THIS LAYER)
3. **Layer 3A**: Retrieve background context (step-back query)
4. **Layer 3B**: Retrieve specific context (original query)
5. **Layer 4**: CRAG evaluation on combined context
6. **Layer 5**: Generate with dual-context reasoning
7. **Layer 6**: Validate answer logic

**Activation Heuristics**:
- Question patterns: "Can I...", "Is it possible...", "Does X work with Y..."
- Query complexity: Medium-high (requires reasoning from principles)
- User tier: Regular/Editor/Admin (useful for educational explanations)

**Example Transformations**:
| Specific Query | Step-Back Query |
|----------------|----------------|
| "Can I trade during combat?" | "What are the general turn phase rules?" |
| "Does expansion X work with Y?" | "How do expansions integrate with base game?" |
| "Can I play this card here?" | "What are the card placement rules?" |

## Sources

- [Step-Back Prompting](https://hub.athina.ai/research-papers/query-transformations-rewriting-step-back-prompting-and-sub-query-decomposition/)
- [Abstraction Prompting for Reasoning](https://arxiv.org/abs/2310.03117)
