# Iterative RAG

**Stats**: 6,736 tokens/query (avg 2.3 iterations) | $0.025/query | +14% accuracy | 2-5s latency | **Priority: P1**

## Architecture

```
Iteration 1: Generate Answer (2,950 tokens)
    ↓
┌────────────────────────────────────────┐
│ Evaluation Phase                       │
│ Input: Answer + Context (3,100 tokens) │
│ Output: Confidence + Refined Query     │
│ Decision: Confident? → DONE            │
│          Uncertain? → ITERATE          │
└────────────────────────────────────────┘
    ↓
Iteration 2 (70% of queries): Re-retrieve + Re-generate (3,810 tokens)
    ↓
Iteration 3 (20% of queries): Further refinement (4,320 tokens)
    ↓
Weighted Average: 6,736 tokens
```

## How It Works

Iterative RAG implements a feedback loop where the system evaluates its own answer, identifies knowledge gaps, refines the query, and retrieves additional context until confident. This self-improving process dramatically reduces hallucinations and increases answer quality for ambiguous or incomplete queries.

The workflow: (1) generate initial answer from retrieved context, (2) evaluate answer confidence and identify gaps ("Is the rule for 2-player or all player counts?"), (3) if confident, return answer; if uncertain, generate refined query targeting the gap, (4) retrieve additional context with refined query, (5) re-generate answer incorporating both original and new context, (6) repeat until confident or max iterations (typically 3).

The evaluation phase uses a smaller, cheaper model (Haiku) to assess confidence, saving tokens compared to using Sonnet for evaluation. The refined query is more specific than the original ("Setup rules for 2-player Wingspan" vs "How to set up Wingspan"), producing better retrieval in subsequent iterations.

For board game rules with context-dependent answers (e.g., rules varying by player count, expansion, or edition), iterative RAG ensures comprehensive answers by progressively filling knowledge gaps.

## Token Breakdown

**Iteration 1** (100% of queries):
- Input: 400 + 50 + 2,500 = 2,950 tokens
- Output: 300 tokens

**Evaluation** (after each iteration):
- Input: 300 (eval prompt) + 300 (answer) + 2,500 (context) = 3,100 tokens
- Output: 100 tokens (confidence score + refined query)

**Iteration 2** (70% of queries):
- Input: 400 + 60 (refined query) + 3,000 (new context) = 3,460 tokens
- Output: 350 tokens

**Iteration 3** (20% of queries):
- Input: 400 + 70 (further refined) + 3,500 = 3,970 tokens
- Output: 350 tokens

**Weighted Average**:
- 1 iteration (30%): 3,250 tokens
- 2 iterations (50%): 6,710 tokens
- 3 iterations (20%): 10,680 tokens
- **Average**: 0.30×3,250 + 0.50×6,710 + 0.20×10,680 = **6,736 tokens**

## When to Use

- **Ambiguous queries** lacking specificity (e.g., "How to play?" without game/mode context)
- **Incomplete initial retrieval** where first-pass chunks miss critical information
- **High-stakes answers** where accuracy justifies iterative refinement (PRECISE tier)

## Code Example

```python
from anthropic import Anthropic

client = Anthropic(api_key="...")

def iterative_rag(query: str, max_iterations: int = 3) -> dict:
    """RAG with iterative refinement until confident or max iterations."""

    iteration = 1
    refined_query = query
    all_context = []
    answer = None

    while iteration <= max_iterations:
        # Retrieve context for current query
        embedding = get_embedding(refined_query)
        chunks = vector_search(embedding, top_k=5)
        all_context.extend(chunks)

        # Deduplicate context across iterations
        unique_context = deduplicate_chunks(all_context)
        context_text = "\n\n".join([c['text'] for c in unique_context])

        # Generate answer
        response = client.messages.create(
            model="claude-3-5-sonnet-20241022",
            max_tokens=350,
            messages=[{
                "role": "user",
                "content": f"Question: {query}\n\nContext:\n{context_text}\n\nAnswer:"
            }]
        )
        answer = response.content[0].text

        # Evaluate confidence
        evaluation = client.messages.create(
            model="claude-3-5-haiku-20241022",  # Cheaper model for eval
            max_tokens=100,
            messages=[{
                "role": "user",
                "content": f"""Evaluate this answer's completeness.

Question: {query}
Answer: {answer}
Context: {context_text[:1000]}...

Rate confidence (0-100):
Identify gaps (if any):
Refined query (if gaps exist):

Format: {{"confidence": 85, "gaps": "Missing 2-player setup", "refined_query": "Setup rules for 2-player Wingspan"}}
"""
            }]
        )

        import json
        eval_result = json.loads(evaluation.content[0].text)

        # Check if confident or max iterations reached
        if eval_result['confidence'] >= 80 or iteration == max_iterations:
            return {
                "answer": answer,
                "iterations": iteration,
                "confidence": eval_result['confidence']
            }

        # Refine query for next iteration
        refined_query = eval_result.get('refined_query', query)
        iteration += 1

    return {"answer": answer, "iterations": max_iterations, "confidence": 50}
```

## Integration

Iterative RAG integrates as a **flow pattern** in TOMAC-RAG's Modular framework, wrapping Layers 3-5 in a feedback loop.

**Standard Flow** (Single-Pass):
```
Layer 3: Retrieve → Layer 4: CRAG → Layer 5: Generate
```

**Iterative Flow** (THIS VARIANT):
```
Loop (max 3 iterations):
  ├─ Layer 3: Retrieve (refined query)
  ├─ Layer 5: Generate answer
  ├─ Evaluation: Assess confidence
  └─ If confident → BREAK
      Else → Refine query → CONTINUE
```

**Optimization Strategies**:
- Use Haiku for evaluation phase (saves ~2,400 tokens per evaluation)
- Limit max iterations to 2 for non-PRECISE tier (saves ~800 tokens on 20% of queries)
- Skip iteration for simple queries (complexity <3)

**Expected Distribution**:
- FAST tier: 0 iterations (single-pass only)
- BALANCED tier: 1-2 iterations (stop at confidence ≥70)
- PRECISE tier: 1-3 iterations (stop at confidence ≥80)

## Sources

- [Modular RAG - Iterative Retrieval](https://arxiv.org/html/2407.21059v1)
- [Self-Reflective RAG](https://www.marktechpost.com/2024/11/25/retrieval-augmented-generation-rag-deep-dive-into-25-different-types-of-rag/)
