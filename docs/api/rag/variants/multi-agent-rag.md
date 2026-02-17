# Multi-Agent RAG

**Stats**: 12,900 tokens/query | $0.043/query (mixed models) | +20% accuracy | 5-10s latency | **Priority: P2**

## Architecture

```
Shared Retrieval: 5,000 tokens (10 chunks)
    ↓
┌─────────────────────────────────────────────────────────┐
│ Agent 1: Retrieval Agent (Haiku)                        │
│ Input: 1,450 tokens | Output: 200 tokens                │
│ Role: Analyze query, plan retrieval strategy            │
└─────────────────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────────────────┐
│ Agent 2: Analysis Agent (Haiku)                         │
│ Input: 3,250 tokens (state + docs) | Output: 400 tokens │
│ Role: Extract relevant rules, identify patterns         │
└─────────────────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────────────────┐
│ Agent 3: Synthesis Agent (Sonnet)                       │
│ Input: 3,650 tokens | Output: 500 tokens                │
│ Role: Synthesize comprehensive answer                   │
└─────────────────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────────────────┐
│ Agent 4: Validation Agent (Haiku)                       │
│ Input: 3,150 tokens | Output: 300 tokens                │
│ Role: Verify answer correctness, check citations        │
└─────────────────────────────────────────────────────────┘
```

## How It Works

Multi-Agent RAG decomposes the RAG pipeline into specialized agents, each optimized for a specific subtask. Unlike monolithic approaches where one LLM handles everything, multi-agent systems leverage task-specific expertise, cheaper models for simple tasks, and parallel execution where possible.

The four-agent architecture: (1) **Retrieval Agent** analyzes query complexity and determines retrieval strategy (which indexes, how many chunks, filters), (2) **Analysis Agent** extracts relevant information from retrieved chunks and identifies patterns, (3) **Synthesis Agent** (premium model) combines findings into a coherent, comprehensive answer, (4) **Validation Agent** verifies accuracy, checks citations, and assigns confidence score.

Key advantages: (1) cheaper models (Haiku at $0.25/1M input) handle 75% of tokens (agents 1,2,4), premium model (Sonnet) only for synthesis, (2) specialized prompts per agent improve task performance, (3) parallel execution of independent agents (1+2 can run concurrently).

For complex strategic queries in board games (e.g., "Optimal 4-player opening strategy with River expansion"), multi-agent RAG achieves 95-98% accuracy by systematically analyzing rules, strategies, expansions, and player interactions.

## Token Breakdown

**Shared Retrieval**: 5,000 tokens (10 chunks, shared across agents)

**Agent 1 - Retrieval Agent** (Haiku):
- Input: 400 (system) + 50 (query) + 1,000 (state) = 1,450 tokens
- Output: 200 tokens (retrieval plan)

**Agent 2 - Analysis Agent** (Haiku):
- Input: 500 + 50 + 200 (Agent 1 output) + 2,500 (docs) = 3,250 tokens
- Output: 400 tokens (analysis report)

**Agent 3 - Synthesis Agent** (Sonnet - premium):
- Input: 500 + 50 + 600 (Agents 1+2 outputs) + 2,500 (docs) = 3,650 tokens
- Output: 500 tokens (synthesized answer)

**Agent 4 - Validation Agent** (Haiku):
- Input: 500 + 50 + 1,100 (previous outputs) + 1,500 (docs) = 3,150 tokens
- Output: 300 tokens (validation report)

**Total Input**: 1,450 + 3,250 + 3,650 + 3,150 = **11,500 tokens**
**Total Output**: 200 + 400 + 500 + 300 = **1,400 tokens**
**Grand Total**: **12,900 tokens**

## When to Use

- **PRECISE tier** for Editor/Admin users on complex strategic queries
- **Controversial rules** requiring multi-perspective analysis and validation
- **High-stakes answers** where 95%+ accuracy justifies 6.5x token cost vs naive RAG

## Code Example

```python
from anthropic import Anthropic
from typing import Dict, List

client = Anthropic(api_key="...")

# Agent configurations
AGENTS = {
    "retrieval": {"model": "claude-3-5-haiku-20241022", "max_tokens": 200},
    "analysis": {"model": "claude-3-5-haiku-20241022", "max_tokens": 400},
    "synthesis": {"model": "claude-3-5-sonnet-20241022", "max_tokens": 500},
    "validation": {"model": "claude-3-5-haiku-20241022", "max_tokens": 300}
}

def multi_agent_rag(query: str) -> Dict:
    """Multi-agent RAG with specialized agents for each phase."""

    # Shared retrieval (executed once)
    query_embedding = get_embedding(query)
    chunks = vector_search(query_embedding, top_k=10)
    context = "\n\n".join([c['text'] for c in chunks])

    # Agent 1: Retrieval Strategy
    retrieval_response = client.messages.create(
        **AGENTS['retrieval'],
        messages=[{
            "role": "user",
            "content": f"""Analyze this query and assess retrieval quality.

Query: {query}
Retrieved chunks: {len(chunks)}

Evaluate:
1. Are the chunks relevant?
2. Is additional retrieval needed?
3. What metadata filters would improve results?

Output JSON: {{"relevant": true/false, "suggestions": "..."}}
"""
        }]
    )

    # Agent 2: Rule Analysis
    analysis_response = client.messages.create(
        **AGENTS['analysis'],
        messages=[{
            "role": "user",
            "content": f"""Extract and analyze relevant rules.

Query: {query}
Context: {context[:2000]}...

Extract:
1. Primary rules relevant to query
2. Edge cases or exceptions
3. Related mechanics

Format as structured analysis.
"""
        }]
    )

    # Agent 3: Answer Synthesis
    synthesis_response = client.messages.create(
        **AGENTS['synthesis'],
        messages=[{
            "role": "user",
            "content": f"""Synthesize comprehensive answer.

Query: {query}
Retrieval Assessment: {retrieval_response.content[0].text}
Rule Analysis: {analysis_response.content[0].text}
Full Context: {context}

Provide a comprehensive, well-structured answer with citations.
"""
        }]
    )

    # Agent 4: Validation
    validation_response = client.messages.create(
        **AGENTS['validation'],
        messages=[{
            "role": "user",
            "content": f"""Validate answer correctness.

Query: {query}
Answer: {synthesis_response.content[0].text}
Context: {context[:1500]}...

Check:
1. Are all claims supported by context?
2. Are citations accurate?
3. Are there contradictions?

Output: {{"confidence": 0-100, "issues": [...], "verified": true/false}}
"""
        }]
    )

    import json
    validation = json.loads(validation_response.content[0].text)

    return {
        "answer": synthesis_response.content[0].text,
        "confidence": validation['confidence'],
        "validated": validation['verified'],
        "agent_outputs": {
            "retrieval": retrieval_response.content[0].text,
            "analysis": analysis_response.content[0].text,
            "validation": validation_response.content[0].text
        }
    }
```

## Integration

Multi-Agent RAG integrates as an **advanced flow pattern** in TOMAC-RAG's Modular framework, replacing Layers 3-6 with a specialized agent pipeline.

**Standard Flow** (Single LLM):
```
Layer 3: Retrieve → Layer 4: CRAG → Layer 5: Generate → Layer 6: Validate
```

**Multi-Agent Flow** (THIS VARIANT):
```
┌─ Agent 1: Retrieval Strategy ─┐
│  (Plan optimal retrieval)      │
└────────────┬───────────────────┘
             ↓
┌─ Agent 2: Analysis ────────────┐  (Can run parallel with Agent 1)
│  (Extract relevant rules)      │
└────────────┬───────────────────┘
             ↓
┌─ Agent 3: Synthesis ───────────┐
│  (Premium model for quality)   │
└────────────┬───────────────────┘
             ↓
┌─ Agent 4: Validation ──────────┐
│  (Verify correctness)          │
└────────────────────────────────┘
```

**Activation Criteria** (PRECISE tier only):
- Query complexity score ≥8 (multi-concept, strategic planning)
- User role: Editor or Admin
- Query template: `resource_planning`, `strategic_analysis`

## Sources

- [Multi-Agent RAG Framework](https://www.mdpi.com/2073-431X/14/12/525)
- [Agentic RAG Patterns](https://www.marktechpost.com/2024/11/25/retrieval-augmented-generation-rag-deep-dive-into-25-different-types-of-rag/)
