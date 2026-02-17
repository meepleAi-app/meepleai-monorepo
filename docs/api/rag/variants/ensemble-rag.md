# Ensemble RAG

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 11,550–20,000 |
| **Cost/Query** | $0.09–$0.15 |
| **Accuracy** | +15–18% above naive |
| **Latency** | 3–8s |
| **Priority** | **P3** - Rare |

---

## Architecture Diagram

```
Query Input
    ↓
[Shared Retrieval] All models use same docs (2,500 tokens)
    ↓
[Parallel Generation] 3-5 LLM models
    ├─ Model 1 (GPT-4o): Generate answer
    ├─ Model 2 (Claude Opus): Generate answer
    ├─ Model 3 (Llama 3 70B): Generate answer
    └─ Model 4 (More models optional)
    ↓
[Consensus/Judge Model] Reconcile answers
    ├─ Compare reasoning
    ├─ Resolve conflicts
    └─ Select best answer
    ↓
Answer (highest confidence)
```

---

## How It Works

1. **Shared Retrieval**: All models use same documents
2. **Parallel Generation**: Each model generates independently
3. **Consensus Voting**: Judge model evaluates all answers
4. **Output Selection**: Highest-confidence or majority-vote answer

---

## Token Breakdown

**Shared Retrieval**: 2,500 tokens (used by all)

**Model 1 (GPT-4o)**: 2,950 input + 300 output = 3,250 tokens
**Model 2 (Claude Opus)**: 2,950 input + 300 output = 3,250 tokens
**Model 3 (Llama 3 70B)**: 2,950 input + 300 output = 3,250 tokens

**Judge Model** (Claude Sonnet):
- Input: 500 (prompt) + 50 (query) + 900 (3 answers) = 1,450 tokens
- Output: 350 tokens

**Total Input**: 2,950 × 3 + 1,450 = 10,300 tokens
**Total Output**: 300 × 3 + 350 = 1,250 tokens
**Total**: 11,550 tokens

**Cost** (multi-provider):
- GPT-4o: $0.0104
- Claude Opus: $0.0681
- Llama 3 70B: $0.0024
- Judge (Sonnet): $0.0096
- **Total**: ~$0.0905 per query

---

## When to Use

✅ **Only For**:
- Critical community disputes
- Controversial rules interpretation
- High-stakes decisions
- When consensus matters more than cost

❌ **Not For**:
- Standard queries (too expensive)
- Latency-sensitive
- Budget-conscious

---

## Code Example

```python
class EnsembleRag:
    async def execute_ensemble(
        self,
        query: str,
        docs: list[Document]
    ) -> EnsembleResult:
        # Run 3 models in parallel
        results = await asyncio.gather(
            self.model_gpt4o.generate(query, docs),
            self.model_opus.generate(query, docs),
            self.model_llama.generate(query, docs)
        )

        # Judge model reconciles
        judge_prompt = f"""
        Three LLM models generated these answers:
        Model 1: {results[0]}
        Model 2: {results[1]}
        Model 3: {results[2]}

        Evaluate which is most accurate and why.
        Select best answer with confidence.
        """

        judgment = await self.judge_model.generate(judge_prompt)

        return EnsembleResult(
            model_1_answer=results[0],
            model_2_answer=results[1],
            model_3_answer=results[2],
            consensus=judgment.best_answer,
            confidence=judgment.confidence
        )
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Accuracy** | +15-18% (best ensemble) | Extremely expensive |
| **Reliability** | Multiple perspectives | 3-8s latency |
| **Cost** | $0.09-0.15 per query | 15-30x naive RAG |

---

## Integration

**Tier Level**: PRECISE tier (rare critical cases only)

---

**Status**: Production-Ready | **MeepleAI Tier**: P3 Rare
