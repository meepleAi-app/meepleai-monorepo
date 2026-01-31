# Adaptive RAG

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | Variable (2,000–15,000) |
| **Cost/Query** | $-$$$ (depends on complexity) |
| **Accuracy** | +10-20% above naive |
| **Latency** | Variable (100ms–10s) |
| **Priority** | **P0** - Core Design |

---

## Architecture Diagram

```
Query Input
    ↓
[Router] Complexity Scoring
    ├─ Simple → FAST path
    ├─ Medium → BALANCED path
    └─ Complex → PRECISE path
    ↓
Strategy-Specific Retriever
    ├─ FAST: MiniLM, top-3
    ├─ BALANCED: E5-Base, top-10
    └─ PRECISE: Multi-hop, top-20
    ↓
Strategy-Specific Grader
    ├─ FAST: None (skip)
    ├─ BALANCED: Cross-encoder
    └─ PRECISE: LLM-based
    ↓
Strategy-Specific Generator
    ├─ FAST: Claude Haiku
    ├─ BALANCED: Claude Sonnet
    └─ PRECISE: Multi-agent
    ↓
Answer Output
```

---

## How It Works

1. **Intent Classification**: Router module analyzes query to determine template (rule_lookup vs resource_planning)
2. **Complexity Scoring**: Calculate 0-5 score based on:
   - Number of concepts mentioned
   - Conditional requirements ("if/then" patterns)
   - Planning/strategic keywords
3. **Strategy Selection**:
   - Score ≤1 → FAST (minimal retrieval, cheap LLM)
   - Score 2-3 → BALANCED (standard retrieval, medium model)
   - Score ≥4 → PRECISE (multi-hop, premium model)
4. **Adaptive Pipeline**: Each tier uses different models, retrieval depths, validation methods
5. **User-Tier Gating**: Cross-check with user role for access control
6. **Escalation Logic**: FAST fails → retry with BALANCED

---

## Token Breakdown

**FAST Path** (60-70% of queries):
- Router: 300 tokens (classification)
- Retriever: 1,500 tokens (3 chunks)
- Generator: 400 tokens (Haiku output)
- Total: ~2,200 tokens

**BALANCED Path** (25-30% of queries):
- Router: 300 tokens
- Retriever: 5,000 tokens (10 chunks)
- Grader: 200 tokens (reranking)
- Generator: 1,200 tokens (Sonnet output)
- Total: ~6,700 tokens

**PRECISE Path** (5-10% of queries):
- Router: 300 tokens
- Retriever: 8,000 tokens (20 chunks, multi-hop)
- Grader: 2,000 tokens (LLM evaluation)
- Generator: 3,500 tokens (multi-agent)
- Total: ~13,800 tokens

**Weighted Average**: 0.65×2,200 + 0.27×6,700 + 0.08×13,800 = **~4,500 tokens/query**

---

## When to Use

✅ **Best For**:
- Multi-game knowledge bases (scale from simple to complex queries)
- Mixed user base (free, paid, admin tiers)
- Cost-conscious deployments (adaptive reduces wasted spend)
- Unknown query complexity distribution

❌ **Not For**:
- Always-critical accuracy needs (use PRECISE tier instead)
- Latency-sensitive (<100ms) applications
- Queries with uniform complexity

---

## Code Example

```python
class AdaptiveRagRouter:
    async def route_query(self, query: str, user_tier: str) -> RagStrategy:
        # Step 1: Classify template
        template = await self.classify_template(query)

        # Step 2: Score complexity
        complexity = self.calculate_complexity(query)

        # Step 3: Select strategy
        strategy = self._select_strategy(complexity)

        # Step 4: Check user access
        if strategy == "PRECISE" and user_tier == "anonymous":
            return "BALANCED"  # Downgrade

        return strategy

    def calculate_complexity(self, query: str) -> int:
        """Score 0-5 based on query characteristics"""
        score = 0

        # Check for multi-concept queries
        concepts = len(self._extract_concepts(query))
        if concepts > 2:
            score += 1

        # Check for conditionals
        if any(keyword in query.lower() for keyword in ["if", "but", "or", "however"]):
            score += 1

        # Check for planning keywords
        if any(keyword in query.lower() for keyword in ["should", "best", "strategy", "optimal"]):
            score += 2

        return min(score, 5)

    def _select_strategy(self, complexity: int) -> str:
        if complexity <= 1:
            return "FAST"
        elif complexity <= 3:
            return "BALANCED"
        else:
            return "PRECISE"
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Cost** | 50-70% savings vs always-PRECISE | Variable (unpredictable bills) |
| **Accuracy** | 80-95% (tier-appropriate) | Not guaranteed high accuracy |
| **Latency** | 100ms (FAST) to 10s (PRECISE) | Escalations add latency |
| **Implementation** | Modular (reuse tiers separately) | Complex router logic required |
| **Scaling** | Handles all query complexities | Requires tuning per domain |

---

## Integration

**Within TOMAC-RAG Framework**:
- Router → Modular flow pattern (conditional)
- FAST → Linear pipeline (no grading)
- BALANCED → Standard 5-tier
- PRECISE → Agentic with multi-agent

**User-Tier Matrix**:
- Anonymous: FAST (80%) → BALANCED (20%)
- User: FAST (60%) → BALANCED (35%) → PRECISE (5%, limited)
- Editor: FAST (45%) → BALANCED (40%) → PRECISE (15%)
- Admin: FAST (25%) → BALANCED (50%) → PRECISE (25%)

---

## Research Sources

- [Advanced RAG Techniques for High-Performance LLM Applications](https://neo4j.com/blog/genai/advanced-rag-techniques/)
- [Building Agentic RAG Systems with LangGraph: The 2026 Guide](https://rahulkolekar.com/building-agentic-rag-systems-with-langgraph/)
- [Query-Adaptive RAG Routing](https://ascii.co.uk/news/article/news-20260122-9ccbfc03/)

---

**Status**: Production-Ready | **MeepleAI Tier**: P0 Design
