# RAPTOR (Recursive Abstractive Processing)

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 4,300 |
| **Cost/Query** | $0.016 |
| **Accuracy** | +10% above naive |
| **Latency** | 1–2s |
| **Priority** | **P3** - Hierarchical |

---

## Architecture Diagram

```
Knowledge Base Preparation (One-time)
    ↓
[Build Summary Tree]
    ├─ Level 1 (Leaves): 100 original chunks (50K tokens)
    ├─ Level 2 (Summaries): 40 summaries of chunks (20K tokens)
    ├─ Level 3 (High-Level): 10 summaries of summaries (5K tokens)
    └─ Amortized: ~0.75 tokens per query
    ↓
[Query Time]
    ├─ Search tree at appropriate abstraction level
    └─ 3,500 tokens (mixed chunks + summaries)
    ↓
[Generation]
    ├─ Input: 400 + 50 + 3,500 = 3,950 tokens
    └─ Output: 350 tokens
    ↓
Answer
```

---

## How It Works

1. **One-Time Tree Building**:
   - Recursively summarize chunks into tree
   - Higher levels = more abstract
   - Cost: One-time 75K tokens

2. **Query-Time Retrieval**:
   - Search tree at multiple levels
   - Combine granular + abstract results
   - Cost: 3,500 tokens

3. **Generation**:
   - Synthesis from diverse abstraction levels
   - Cost: 4,300 tokens total

---

## Token Breakdown

**Tree Construction** (one-time, amortized):
- 75K tokens / 100K queries = 0.75 tokens per query

**Query-Time Retrieval**:
- 3,500 tokens (tree search)

**Generation**:
- 4,300 tokens total

---

## When to Use

✅ **For**:
- Broad + specific queries
- Long-term corpus (amortize build cost)
- When hierarchy matters

❌ **Not For**:
- One-off queries
- Frequently changing corpus

---

## Status

**Status**: Partial Production-Ready | **MeepleAI Tier**: P3 Hierarchical

RAPTOR is excellent for static knowledge bases with mixed granularity needs (overview + details).

---

**Status**: Partial Production-Ready | **MeepleAI Tier**: P3 Hierarchical
