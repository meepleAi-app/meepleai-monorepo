# Adaptive RAG

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | Variable (2,000–22,000) |
| **Cost/Query** | $-$$$ (depends on complexity) |
| **Accuracy** | +10-20% above naive |
| **Latency** | Variable (100ms–10s) |
| **Priority** | **P0** - Core Design |

---

## Architecture Diagram

```
Query Input
    ↓
[Auth Check] → Anonymous? → ❌ REJECT (authentication required)
    ↓
[Router] Complexity Scoring + User Tier
    ├─ Simple → FAST path
    ├─ Medium → BALANCED path
    ├─ Complex → PRECISE path
    ├─ External info needed → EXPERT path (Admin/Premium only)
    ├─ High-stakes decision → CONSENSUS path (Admin/Premium only)
    └─ Admin override → CUSTOM path
    ↓
Strategy-Specific Pipeline
    ├─ FAST: [Synthesis only]
    ├─ BALANCED: [Synthesis + CRAG Evaluation]
    ├─ PRECISE: [Retrieval + Analysis + Synthesis + Validation]
    ├─ EXPERT: [Web Search + Multi-Hop + Synthesis]
    ├─ CONSENSUS: [Voter1 + Voter2 + Voter3 + Aggregator]
    └─ CUSTOM: [Admin-configured phases]
    ↓
Strategy-Specific Generator
    ├─ FAST: Llama 3.3 Free / Haiku
    ├─ BALANCED: GPT-4o-mini / Sonnet
    ├─ PRECISE: Claude Opus / Multi-agent
    ├─ EXPERT: Sonnet + Web augmentation
    ├─ CONSENSUS: Multi-LLM voting (Sonnet + GPT-4o + DeepSeek)
    └─ CUSTOM: Phase-configured models
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

**FAST Path** (~55% of queries for User tier):
- Router: 300 tokens (classification)
- Retriever: 1,500 tokens (3 chunks)
- Generator: 400 tokens (Haiku/Llama output)
- Total: **~2,060 tokens** | Cost: $0.0001 (free models)

**BALANCED Path** (~35% of queries):
- Router: 300 tokens
- Retriever: 3,500 tokens (10 chunks, filtered)
- CRAG Evaluation: 200 tokens
- Generator: 800 tokens (Sonnet output)
- Total: **~2,820 tokens** | Cost: $0.01 (DeepSeek/Sonnet)

**PRECISE Path** (~8% of queries, Editor+):
- Router: 300 tokens
- Retrieval: 2,500 tokens (multi-phase)
- Analysis: 3,000 tokens
- Synthesis: 8,000 tokens (multi-agent)
- Validation: 4,000 tokens
- Total: **~22,396 tokens** | Cost: $0.132 (Haiku + Sonnet + Opus)

**EXPERT Path** (~2% of queries, Admin/Premium only):
- Router: 300 tokens
- Web Search: 3,000 tokens (external sources)
- Multi-Hop: 6,000 tokens (entity expansion, max 3 hops)
- Synthesis: 5,000 tokens
- Total: **~15,000 tokens** | Cost: $0.099 (Claude Sonnet 4.5)

**CONSENSUS Path** (~1% of queries, Admin/Premium only):
- Router: 300 tokens
- Voter 1 (Sonnet): 4,500 tokens
- Voter 2 (GPT-4o): 4,500 tokens
- Voter 3 (DeepSeek): 4,500 tokens
- Aggregator: 3,500 tokens
- Total: **~18,000 tokens** | Cost: $0.09 (Multi-LLM)

**CUSTOM Path** (<1% of queries, Admin only):
- Variable based on phase configuration
- Total: **Variable** | Cost: Variable

**Weighted Average** (typical distribution): **~4,200 tokens/query**

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
        # Anonymous users cannot access the system - authentication required
        if user_tier == "anonymous":
            raise AuthenticationRequiredException("Authentication required")

        if strategy == "PRECISE" and user_tier == "user":
            return "BALANCED"  # Downgrade for User tier

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
> **Note**: Anonymous users cannot access the RAG system. Authentication is required.

- ~~Anonymous~~: ❌ NO ACCESS (authentication required)
- User: FAST (60%) → BALANCED (40%)
- Editor: FAST (45%) → BALANCED (40%) → PRECISE (15%)
- Admin: FAST (25%) → BALANCED (40%) → PRECISE (20%) → EXPERT/CONSENSUS (15%)
- Premium: FAST (20%) → BALANCED (35%) → PRECISE (25%) → EXPERT/CONSENSUS (20%)

---

## Research Sources

- [Advanced RAG Techniques for High-Performance LLM Applications](https://neo4j.com/blog/genai/advanced-rag-techniques/)
- [Building Agentic RAG Systems with LangGraph: The 2026 Guide](https://rahulkolekar.com/building-agentic-rag-systems-with-langgraph/)
- [Query-Adaptive RAG Routing](https://ascii.co.uk/news/article/news-20260122-9ccbfc03/)

---

**Status**: Production-Ready | **MeepleAI Tier**: P0 Design
